using ExamBox.Data;
using ExamBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Services;

public sealed class StudentHome
{
    public User Student { get; init; } = null!;
    public List<Exam> Available { get; init; } = new();
    public List<Exam> Upcoming { get; init; } = new();
    public List<Attempt> InProgress { get; init; } = new();
    public List<Attempt> Completed { get; init; } = new();
}

public sealed class TakeData
{
    public Attempt Attempt { get; init; } = null!;
    public Exam Exam { get; init; } = null!;
    /// <summary>Objective questions first, then theory, in the order this student sees them.</summary>
    public List<Question> Questions { get; init; } = new();
    public Dictionary<int, Answer> Saved { get; init; } = new();
    public int SecondsLeft { get; init; }
    public bool Paused { get; init; }
}

/// <summary>Everything a student does with an exam: see it, start it, answer it, hand it in.</summary>
public sealed class AttemptService(DbFactory factory)
{
    private readonly ActivityLog _log = new(factory);

    public const int GraceSeconds = 30;
    public const int MaxTheoryChars = 20000;

    /// <summary>When this attempt runs out. Pauses give time back and admins can adjust it per student.</summary>
    public static DateTime Deadline(Attempt a, Exam e, DateTime? now = null)
    {
        var end = a.StartedAt.AddMinutes(e.DurationMinutes).AddSeconds(a.PausedSeconds + a.TimeAdjustSeconds);
        if (a.PausedAt != null) end += (now ?? DateTime.UtcNow) - a.PausedAt.Value;      // clock is frozen while paused
        // the exam's closing time only caps students who were never given extra or paused time
        var touched = a.PausedAt != null || a.PausedSeconds != 0 || a.TimeAdjustSeconds != 0;
        return !touched && e.ClosesAt is { } c && c < end ? c : end;
    }

    public static int SecondsLeft(Attempt a, Exam e) => (int)(Deadline(a, e) - DateTime.UtcNow).TotalSeconds;

    private static bool Eligible(Exam e, User s) =>
        string.IsNullOrEmpty(e.ForDepartment) || string.Equals(e.ForDepartment, s.Department, StringComparison.OrdinalIgnoreCase);

    public StudentHome Home(int studentId)
    {
        CollectExpired(studentId);
        using var db = factory.Create();
        var student = db.Users.AsNoTracking().First(u => u.Id == studentId);
        var attempts = db.Attempts.AsNoTracking().Include(a => a.Exam).Where(a => a.StudentId == studentId)
            .OrderByDescending(a => a.StartedAt).ToList();
        var taken = attempts.Select(a => a.ExamId).ToHashSet();
        var now = DateTime.UtcNow;
        var live = db.Exams.AsNoTracking().Include(e => e.Questions)
            .Where(e => e.IsPublished && !taken.Contains(e.Id)).OrderBy(e => e.Title).ToList()
            .Where(e => e.Questions.Count > 0 && Eligible(e, student)).ToList();
        return new StudentHome
        {
            Student = student,
            Available = live.Where(e => e.StateAt(now) == ExamState.Open).ToList(),
            Upcoming = live.Where(e => e.StateAt(now) == ExamState.Scheduled).OrderBy(e => e.OpensAt).ToList(),
            InProgress = attempts.Where(a => a.SubmittedAt == null).ToList(),
            Completed = attempts.Where(a => a.SubmittedAt != null).ToList(),
        };
    }

    public OpResult<int> Start(int studentId, int examId)
    {
        using var db = factory.Create();
        var student = db.Users.AsNoTracking().FirstOrDefault(u => u.Id == studentId);
        var exam = db.Exams.Include(e => e.Questions).FirstOrDefault(e => e.Id == examId && e.IsPublished);
        if (student == null || exam == null || exam.Questions.Count == 0 || !Eligible(exam, student))
            return OpResult<int>.Fail("This exam is not available to you.");
        var existing = db.Attempts.FirstOrDefault(a => a.ExamId == examId && a.StudentId == studentId);
        if (existing != null) return OpResult<int>.Success(existing.Id);
        var now = DateTime.UtcNow;
        switch (exam.StateAt(now))
        {
            case ExamState.Scheduled: return OpResult<int>.Fail($"This exam opens {exam.OpensAt:f} UTC.");
            case ExamState.Closed: return OpResult<int>.Fail("This exam has closed.");
        }
        var attempt = new Attempt { ExamId = examId, StudentId = studentId, StartedAt = now, TotalMarks = exam.Questions.Sum(q => q.Marks) };
        db.Attempts.Add(attempt);
        try { db.SaveChanges(); }
        catch (DbUpdateException) // double-click race on the unique (exam, student) index
        {
            db.ChangeTracker.Clear();
            attempt = db.Attempts.First(a => a.ExamId == examId && a.StudentId == studentId);
        }
        _log.Write("exam", student.Username, "Exam started", exam.Title);
        return OpResult<int>.Success(attempt.Id);
    }

    /// <summary>The exam for an unfinished attempt; null when it does not exist, is not this student's, or is already submitted.</summary>
    public TakeData? GetTake(int studentId, int attemptId)
    {
        CollectExpired(studentId);
        using var db = factory.Create();
        var a = db.Attempts.AsNoTracking().Include(x => x.Exam).ThenInclude(e => e!.Questions).Include(x => x.Answers)
            .FirstOrDefault(x => x.Id == attemptId && x.StudentId == studentId);
        if (a == null || a.SubmittedAt != null) return null;
        var left = (int)(Deadline(a, a.Exam!) - DateTime.UtcNow).TotalSeconds;
        return new TakeData
        {
            Attempt = a, Exam = a.Exam!, Questions = Arrange(a.Exam!, a), SecondsLeft = Math.Max(0, left), Paused = a.PausedAt != null,
            Saved = a.Answers.ToDictionary(x => x.QuestionId),
        };
    }

    /// <summary>The student continues a paused exam; the clock starts again with the time that was left.</summary>
    public OpResult Resume(int studentId, int attemptId)
    {
        using var db = factory.Create();
        var a = db.Attempts.FirstOrDefault(x => x.Id == attemptId && x.StudentId == studentId && x.SubmittedAt == null);
        if (a == null) return OpResult.Fail("Attempt not found.");
        if (a.PausedAt != null)
        {
            a.PausedSeconds += (int)(DateTime.UtcNow - a.PausedAt.Value).TotalSeconds;
            a.PausedAt = null;
            db.SaveChanges();
        }
        return OpResult.Success();
    }

    public bool IsSubmitted(int studentId, int attemptId)
    {
        using var db = factory.Create();
        return db.Attempts.Any(x => x.Id == attemptId && x.StudentId == studentId && x.SubmittedAt != null);
    }

    public static List<Question> Arrange(Exam e, Attempt a)
    {
        var qs = e.Questions.OrderBy(q => q.SortOrder).ThenBy(q => q.Id).ToList();
        IEnumerable<Question> obj = qs.Where(q => q.Type == QuestionType.Objective);
        if (e.ShuffleQuestions) obj = obj.OrderBy(q => Mix(a.Id, q.Id));
        return obj.Concat(qs.Where(q => q.Type == QuestionType.Theory)).ToList();
    }

    private static long Mix(int a, int b)
    {
        unchecked
        {
            var h = (ulong)a * 0x9E3779B97F4A7C15UL ^ (ulong)b * 0xC2B2AE3D27D4EB4FUL;
            h ^= h >> 29; h *= 0xBF58476D1CE4E5B9UL; h ^= h >> 32;
            return (long)(h >> 1);
        }
    }

    /// <summary>Autosave. Returns seconds left, or an error when the attempt is gone or finished.</summary>
    public OpResult<int> SaveDraft(int studentId, int attemptId, IReadOnlyDictionary<int, string> answers)
    {
        using var db = factory.Create();
        var a = db.Attempts.Include(x => x.Exam).ThenInclude(e => e!.Questions).Include(x => x.Answers)
            .FirstOrDefault(x => x.Id == attemptId && x.StudentId == studentId);
        if (a == null) return OpResult<int>.Fail("Attempt not found.");
        if (a.SubmittedAt != null) return OpResult<int>.Fail("This exam has already been submitted.");
        if (a.PausedAt != null) return OpResult<int>.Fail("paused");
        var left = (int)(Deadline(a, a.Exam!) - DateTime.UtcNow).TotalSeconds;
        if (left < -GraceSeconds) { Finish(db, a, null); return OpResult<int>.Fail("Time is up."); }
        ApplyAnswers(a, answers);
        db.SaveChanges();
        return OpResult<int>.Success(Math.Max(0, left));
    }

    /// <summary>Hands in the exam. <c>Value</c> is true when it was late (answers beyond the saved drafts were ignored).</summary>
    public OpResult<bool> Submit(int studentId, int attemptId, IReadOnlyDictionary<int, string> answers)
    {
        using var db = factory.Create();
        var a = db.Attempts.Include(x => x.Exam).ThenInclude(e => e!.Questions).Include(x => x.Answers)
            .FirstOrDefault(x => x.Id == attemptId && x.StudentId == studentId);
        if (a == null) return OpResult<bool>.Fail("Attempt not found.");
        if (a.SubmittedAt != null) return OpResult<bool>.Success(false);
        var late = DateTime.UtcNow > Deadline(a, a.Exam!).AddSeconds(GraceSeconds);
        Finish(db, a, late ? null : answers);
        _log.Write("exam", db.Users.Where(u => u.Id == studentId).Select(u => u.Username).FirstOrDefault(), late ? "Exam submitted (time was up)" : "Exam submitted", a.Exam!.Title);
        return OpResult<bool>.Success(late);
    }

    public Attempt? GetResult(int studentId, int attemptId)
    {
        CollectExpired(studentId);
        using var db = factory.Create();
        var a = db.Attempts.AsNoTracking().Include(x => x.Exam).ThenInclude(e => e!.Questions).Include(x => x.Answers)
            .FirstOrDefault(x => x.Id == attemptId && x.StudentId == studentId);
        if (a == null || a.SubmittedAt == null) return null;
        a.Exam!.Questions.Sort(ExamService.Ordering);
        return a;
    }

    /// <summary>Can this student see the picture of this question? (Only if they have an attempt on its exam.)</summary>
    public Question? GetImageFor(int studentId, int questionId)
    {
        using var db = factory.Create();
        var q = db.Questions.AsNoTracking().FirstOrDefault(x => x.Id == questionId && x.ImageData != null);
        if (q == null) return null;
        return db.Attempts.Any(a => a.ExamId == q.ExamId && a.StudentId == studentId) ? q : null;
    }

    /// <summary>Submits every unfinished attempt (of one student, or everyone) whose time is up, using the saved drafts.</summary>
    public int CollectExpired(int? studentId = null)
    {
        using var db = factory.Create();
        var q = db.Attempts.Include(a => a.Exam).ThenInclude(e => e!.Questions).Include(a => a.Answers)
            .Where(a => a.SubmittedAt == null);
        if (studentId != null) q = q.Where(a => a.StudentId == studentId);
        var n = 0;
        foreach (var a in q.ToList().Where(a => a.PausedAt == null && DateTime.UtcNow > Deadline(a, a.Exam!)))
        {
            Finish(db, a, null);
            _log.Write("exam", db.Users.Where(u => u.Id == a.StudentId).Select(u => u.Username).FirstOrDefault(), "Exam collected (time ran out)", a.Exam!.Title);
            n++;
        }
        return n;
    }

    // ---- internals ----

    private static void ApplyAnswers(Attempt a, IReadOnlyDictionary<int, string> given)
    {
        foreach (var q in a.Exam!.Questions)
        {
            if (!given.TryGetValue(q.Id, out var v)) continue;
            var row = a.Answers.FirstOrDefault(x => x.QuestionId == q.Id);
            if (row == null) { row = new Answer { QuestionId = q.Id }; a.Answers.Add(row); }
            if (q.Type == QuestionType.Objective)
                row.Selected = q.Options().Any(o => o.Key == v) ? v : null; // ignore tampered values
            else
            {
                v ??= "";
                row.Text = v.Length > MaxTheoryChars ? v[..MaxTheoryChars] : v;
            }
        }
    }

    private static void Finish(AppDb db, Attempt a, IReadOnlyDictionary<int, string>? answers)
    {
        if (answers != null) ApplyAnswers(a, answers);
        var obj = 0; var anyToMark = false;
        foreach (var q in a.Exam!.Questions)
        {
            var row = a.Answers.FirstOrDefault(x => x.QuestionId == q.Id);
            if (row == null) { row = new Answer { QuestionId = q.Id }; a.Answers.Add(row); }
            if (q.Type == QuestionType.Objective)
            {
                if (row.Selected != null && row.Selected == q.CorrectOption) obj += q.Marks;
            }
            else if (q.Marks > 0)
            {
                if (string.IsNullOrWhiteSpace(row.Text)) row.Marks = 0; // nothing typed: nothing to mark
                else { anyToMark = true; row.Marks = null; }
            }
        }
        a.ObjectiveScore = obj; a.TheoryScore = 0; a.Score = obj;
        a.TotalMarks = a.Exam.Questions.Sum(q => q.Marks);
        a.PendingMarking = anyToMark;
        a.SubmittedAt = DateTime.UtcNow;
        db.SaveChanges();
    }
}
