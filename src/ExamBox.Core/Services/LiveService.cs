using ExamBox.Data;
using ExamBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Services;

public sealed record LiveRow(int AttemptId, int StudentId, string Student, string StudentCode, DateTime StartedUtc, int SecondsLeft, bool Paused,
    bool Began, int Current, int TotalQuestions, int Answered, int ObjectiveQuestions, int Score, int MaxScore);

/// <summary>What an admin can do to an exam a student is currently sitting, and how they are getting on.</summary>
public sealed class LiveService(DbFactory factory)
{
    private readonly ActivityLog _log = new(factory);

    private string Who(AppDb db, Attempt a) => db.Users.Where(u => u.Id == a.StudentId).Select(u => u.Username).FirstOrDefault() + " / " + db.Exams.Where(e => e.Id == a.ExamId).Select(e => e.Title).FirstOrDefault();

    public List<LiveRow> List(int examId)
    {
        using var db = factory.Create();
        var list = db.Attempts.AsNoTracking().Include(a => a.Student).Include(a => a.Answers).Include(a => a.Exam).ThenInclude(e => e!.Questions)
            .Where(a => a.ExamId == examId && a.SubmittedAt == null).OrderBy(a => a.StartedAt).ToList();
        return list.Select(a =>
        {
            var obj = a.Exam!.Questions.Where(q => q.Type == QuestionType.Objective).ToList();
            var byQ = a.Answers.ToDictionary(x => x.QuestionId);
            var answered = obj.Count(q => byQ.TryGetValue(q.Id, out var r) && r.Selected != null);
            var score = obj.Where(q => byQ.TryGetValue(q.Id, out var r) && r.Selected != null && r.Selected == q.CorrectOption).Sum(q => q.Marks);
            return new LiveRow(a.Id, a.StudentId, a.Student!.FullName, a.Student.Username, a.StartedAt, Math.Max(0, AttemptService.SecondsLeft(a, a.Exam!)),
                a.PausedAt != null, a.BeganAt != null, a.CurrentQuestion, a.Exam.Questions.Count, answered, obj.Count, score, obj.Sum(q => q.Marks));
        }).ToList();
    }

    /// <summary>Freezes the clock and signs the student out. When they sign back in the exam is waiting with the time left.</summary>
    public OpResult Pause(int attemptId)
    {
        using var db = factory.Create();
        var a = db.Attempts.FirstOrDefault(x => x.Id == attemptId && x.SubmittedAt == null);
        if (a == null) return OpResult.Fail("This exam is not running.");
        if (a.PausedAt == null) a.PausedAt = DateTime.UtcNow;
        db.Users.Where(u => u.Id == a.StudentId).ExecuteUpdate(s => s.SetProperty(u => u.SessionVersion, u => u.SessionVersion + 1));
        db.SaveChanges();
        _log.Write("exam", "Admin", "Exam paused for a student", Who(db, a));
        return OpResult.Success();
    }

    /// <summary>Lets a paused exam run again from the admin side.</summary>
    public OpResult Resume(int attemptId)
    {
        using var db = factory.Create();
        var a = db.Attempts.FirstOrDefault(x => x.Id == attemptId && x.SubmittedAt == null);
        if (a == null) return OpResult.Fail("This exam is not running.");
        if (a.PausedAt != null) { a.PausedSeconds += (int)(DateTime.UtcNow - a.PausedAt.Value).TotalSeconds; a.PausedAt = null; db.SaveChanges(); _log.Write("exam", "Admin", "Exam resumed by admin", Who(db, a)); }
        return OpResult.Success();
    }

    /// <summary>Sets how many minutes this student has left from now (works while running or paused).</summary>
    public OpResult SetMinutesLeft(int attemptId, int minutes)
    {
        if (minutes is < 0 or > 1440) return OpResult.Fail("Enter between 0 and 1440 minutes.");
        using var db = factory.Create();
        var a = db.Attempts.Include(x => x.Exam).FirstOrDefault(x => x.Id == attemptId && x.SubmittedAt == null);
        if (a == null) return OpResult.Fail("This exam is not running.");
        var now = DateTime.UtcNow;
        var current = (int)(AttemptService.Deadline(a, a.Exam!, now) - now).TotalSeconds;
        // a paused attempt keeps its deadline moving with the clock, so compare against the frozen remainder instead
        if (a.PausedAt != null) current = (int)(AttemptService.Deadline(a, a.Exam!, a.PausedAt.Value) - a.PausedAt.Value).TotalSeconds;
        if (a.BeganAt == null) current = a.Exam!.DurationMinutes * 60;
        a.TimeAdjustSeconds += minutes * 60 - current;
        db.SaveChanges();
        _log.Write("exam", "Admin", $"Time left set to {minutes} min", Who(db, a));
        return OpResult.Success();
    }

    /// <summary>Hands in every exam still running for this exam (answers saved so far count). Returns how many were submitted.</summary>
    public int SubmitAll(int examId)
    {
        var attempts = new AttemptService(factory);
        List<int> ids;
        using (var db = factory.Create()) ids = db.Attempts.Where(a => a.ExamId == examId && a.SubmittedAt == null).Select(a => a.Id).ToList();
        foreach (var id in ids) attempts.AdminSubmit(id);
        return ids.Count;
    }
}
