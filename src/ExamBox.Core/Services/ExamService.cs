using ExamBox.Data;
using ExamBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Services;

public sealed class ExamService(DbFactory factory)
{
    public List<Exam> List()
    {
        using var db = factory.Create();
        return db.Exams.AsNoTracking().Include(e => e.Questions).Include(e => e.Attempts)
            .OrderByDescending(e => e.CreatedAt).ToList();
    }

    public Exam? Get(int id)
    {
        using var db = factory.Create();
        var e = db.Exams.AsNoTracking().Include(x => x.Questions).Include(x => x.Attempts).ThenInclude(a => a.Student)
            .FirstOrDefault(x => x.Id == id);
        e?.Questions.Sort(Ordering);
        return e;
    }

    public static int Ordering(Question a, Question b)
    {
        var c = a.SortOrder.CompareTo(b.SortOrder);
        return c != 0 ? c : a.Id.CompareTo(b.Id);
    }

    public bool IsLocked(int examId)
    {
        using var db = factory.Create();
        return db.Attempts.Any(a => a.ExamId == examId);
    }

    /// <summary>Creates (Id == 0) or updates an exam's details. Publish state is changed only via <see cref="SetPublished"/>.</summary>
    public OpResult<Exam> Save(int id, string title, string? description, int durationMinutes, int passMarkPercent,
        string? forDepartment = null, bool shuffle = false, bool showCorrect = true)
    {
        title = title?.Trim() ?? "";
        if (title.Length == 0) return OpResult<Exam>.Fail("Enter a title.");
        if (title.Length > 160) return OpResult<Exam>.Fail("Title is too long.");
        if (durationMinutes is < 1 or > 600) return OpResult<Exam>.Fail("Duration must be between 1 and 600 minutes.");
        if (passMarkPercent is < 1 or > 100) return OpResult<Exam>.Fail("Pass mark must be between 1 and 100.");
        using var db = factory.Create();
        Exam e;
        if (id == 0) { e = new Exam(); db.Exams.Add(e); }
        else
        {
            e = db.Exams.Find(id)!;
            if (e == null) return OpResult<Exam>.Fail("Exam not found.");
        }
        e.Title = title; e.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        e.DurationMinutes = durationMinutes; e.PassMarkPercent = passMarkPercent;
        e.ForDepartment = string.IsNullOrWhiteSpace(forDepartment) ? null : forDepartment.Trim();
        e.ShuffleQuestions = shuffle; e.ShowCorrectAnswers = showCorrect;
        db.SaveChanges();
        return OpResult<Exam>.Success(e);
    }

    /// <summary>Makes the exam available to students. Pass opensAt = null to open immediately.</summary>
    public OpResult Launch(int id, DateTime? opensAtUtc, DateTime? closesAtUtc, string? forDepartment)
    {
        using var db = factory.Create();
        var e = db.Exams.Include(x => x.Questions).FirstOrDefault(x => x.Id == id);
        if (e == null) return OpResult.Fail("Exam not found.");
        if (e.Questions.Count == 0) return OpResult.Fail("Add at least one question before launching.");
        var missing = e.Questions.Count(q => q.MissingImage);
        if (missing > 0) return OpResult.Fail($"{missing} question{(missing == 1 ? " is" : "s are")} still waiting for a picture. Attach them first.");
        if (opensAtUtc != null && closesAtUtc != null && closesAtUtc <= opensAtUtc) return OpResult.Fail("The closing time must be after the opening time.");
        if (closesAtUtc != null && closesAtUtc <= DateTime.UtcNow) return OpResult.Fail("The closing time is already in the past.");
        e.IsPublished = true;
        e.OpensAt = opensAtUtc; e.ClosesAt = closesAtUtc;
        e.ForDepartment = string.IsNullOrWhiteSpace(forDepartment) ? null : forDepartment.Trim();
        db.SaveChanges();
        return OpResult.Success();
    }

    /// <summary>Takes the exam off the student portal (it goes back to a draft; existing attempts are kept).</summary>
    public OpResult Unpublish(int id)
    {
        using var db = factory.Create();
        var e = db.Exams.Find(id);
        if (e == null) return OpResult.Fail("Exam not found.");
        e.IsPublished = false; e.OpensAt = null; e.ClosesAt = null;
        db.SaveChanges();
        return OpResult.Success();
    }

    /// <summary>Closes a running exam now; unfinished attempts are collected by the portal.</summary>
    public OpResult CloseNow(int id)
    {
        using var db = factory.Create();
        var e = db.Exams.Find(id);
        if (e == null || !e.IsPublished) return OpResult.Fail("Exam is not live.");
        e.ClosesAt = DateTime.UtcNow;
        db.SaveChanges();
        return OpResult.Success();
    }

    /// <summary>Copies an exam with all its questions and pictures into a fresh, unpublished draft.</summary>
    public OpResult<Exam> Duplicate(int id, string? title = null)
    {
        using var db = factory.Create();
        var src = db.Exams.AsNoTracking().Include(x => x.Questions).FirstOrDefault(x => x.Id == id);
        if (src == null) return OpResult<Exam>.Fail("Exam not found.");
        var copy = new Exam
        {
            Title = string.IsNullOrWhiteSpace(title) ? Trunc(src.Title, 152) + " (copy)" : title.Trim(),
            Description = src.Description, DurationMinutes = src.DurationMinutes, PassMarkPercent = src.PassMarkPercent,
            ForDepartment = src.ForDepartment, ShuffleQuestions = src.ShuffleQuestions, ShowCorrectAnswers = src.ShowCorrectAnswers,
        };
        foreach (var q in src.Questions.OrderBy(q => q.SortOrder).ThenBy(q => q.Id))
            copy.Questions.Add(new Question
            {
                Type = q.Type, Number = q.Number, SortOrder = q.SortOrder, Text = q.Text, OptionA = q.OptionA, OptionB = q.OptionB,
                OptionC = q.OptionC, OptionD = q.OptionD, OptionE = q.OptionE, CorrectOption = q.CorrectOption, Marks = q.Marks,
                ModelAnswer = q.ModelAnswer, ImageData = q.ImageData, ImageType = q.ImageType, ImageRequired = q.ImageRequired,
            });
        db.Exams.Add(copy);
        db.SaveChanges();
        return OpResult<Exam>.Success(copy);
    }

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];

    public OpResult Delete(int id)
    {
        using var db = factory.Create();
        var e = db.Exams.Find(id);
        if (e == null) return OpResult.Fail("Exam not found.");
        db.Exams.Remove(e);
        db.SaveChanges();
        return OpResult.Success();
    }

    // ---- questions: locked once any student has started, so grading stays consistent ----

    public const int MaxImageBytes = 3 * 1024 * 1024;
    private const string Locked = "Students have already started this exam; its questions are locked.";

    /// <summary>Checks a question and returns an error message, or null when it is fine.</summary>
    public static string? Validate(QuestionInput i)
    {
        if (string.IsNullOrWhiteSpace(i.Text)) return "Enter the question text.";
        if (i.Text.Trim().Length > 4000) return "Question text is too long.";
        if (i.Marks > 100 || i.Marks < (i.Type == QuestionType.Theory ? 0 : 1)) return i.Type == QuestionType.Theory ? "Marks must be between 0 and 100 (0 makes it a reading passage that is not scored)." : "Marks must be between 1 and 100.";
        if (i.Number is { Length: > 12 }) return "The question number is too long.";
        if (i.Type == QuestionType.Theory) return null;
        var opts = new[] { i.A, i.B, i.C, i.D, i.E }.Select(o => string.IsNullOrWhiteSpace(o) ? null : o.Trim()).ToArray();
        if (opts[0] == null || opts[1] == null) return "Options A and B are required.";
        for (var k = 2; k < 5; k++)
            if (opts[k] != null && opts[k - 1] == null) return $"Fill option {(char)('A' + k - 1)} before option {(char)('A' + k)}.";
        if (opts.Any(o => o is { Length: > 500 })) return "An option is too long (500 characters at most).";
        var c = (i.Correct ?? "").Trim().ToUpperInvariant();
        if (c.Length != 1 || c[0] < 'A' || c[0] > 'E') return "Choose the correct answer.";
        if (opts[c[0] - 'A'] == null) return "The correct answer must be one of the filled-in options.";
        return null;
    }

    /// <summary>Copies validated input onto a question entity.</summary>
    public static void Apply(Question q, QuestionInput i)
    {
        q.Type = i.Type;
        q.Number = string.IsNullOrWhiteSpace(i.Number) ? null : i.Number.Trim();
        q.Text = i.Text.Trim();
        q.Marks = i.Marks;
        q.ModelAnswer = string.IsNullOrWhiteSpace(i.ModelAnswer) ? null : i.ModelAnswer.Trim();
        q.ImageRequired = i.ImageRequired;
        static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        if (i.Type == QuestionType.Theory)
        {
            q.OptionA = ""; q.OptionB = ""; q.OptionC = q.OptionD = q.OptionE = null; q.CorrectOption = null;
        }
        else
        {
            q.OptionA = T(i.A)!; q.OptionB = T(i.B)!; q.OptionC = T(i.C); q.OptionD = T(i.D); q.OptionE = T(i.E);
            q.CorrectOption = i.Correct!.Trim().ToUpperInvariant();
        }
    }

    public OpResult SaveQuestion(int examId, int questionId, QuestionInput input)
    {
        var err = Validate(input);
        if (err != null) return OpResult.Fail(err);
        using var db = factory.Create();
        if (!db.Exams.Any(e => e.Id == examId)) return OpResult.Fail("Exam not found.");
        if (db.Attempts.Any(x => x.ExamId == examId)) return OpResult.Fail(Locked);
        Question q;
        if (questionId == 0)
        {
            var next = db.Questions.Where(x => x.ExamId == examId).Select(x => (int?)x.SortOrder).Max() ?? 0;
            q = new Question { ExamId = examId, SortOrder = next + 1 };
            db.Questions.Add(q);
        }
        else
        {
            q = db.Questions.FirstOrDefault(x => x.Id == questionId && x.ExamId == examId)!;
            if (q == null) return OpResult.Fail("Question not found.");
        }
        Apply(q, input);
        db.SaveChanges();
        return OpResult.Success();
    }

    /// <summary>Adds many questions at once (import). <paramref name="replace"/> clears the existing ones first.</summary>
    public OpResult<int> AddQuestions(int examId, IReadOnlyList<Question> questions, bool replace)
    {
        using var db = factory.Create();
        if (!db.Exams.Any(e => e.Id == examId)) return OpResult<int>.Fail("Exam not found.");
        if (db.Attempts.Any(x => x.ExamId == examId)) return OpResult<int>.Fail(Locked);
        if (replace) db.Questions.RemoveRange(db.Questions.Where(x => x.ExamId == examId));
        var next = replace ? 0 : db.Questions.Where(x => x.ExamId == examId).Select(x => (int?)x.SortOrder).Max() ?? 0;
        foreach (var q in questions)
        {
            q.Id = 0; q.ExamId = examId; q.SortOrder = ++next;
            db.Questions.Add(q);
        }
        db.SaveChanges();
        return OpResult<int>.Success(questions.Count);
    }

    public OpResult SetImage(int examId, int questionId, byte[] data, string contentType)
    {
        if (data.Length == 0) return OpResult.Fail("The picture is empty.");
        if (data.Length > MaxImageBytes) return OpResult.Fail("The picture is larger than 3 MB. Shrink it and try again.");
        var type = ImageSniffer.Detect(data);
        if (type == null) return OpResult.Fail("Use a PNG, JPG, GIF or WebP picture.");
        using var db = factory.Create();
        if (db.Attempts.Any(x => x.ExamId == examId)) return OpResult.Fail(Locked);
        var q = db.Questions.FirstOrDefault(x => x.Id == questionId && x.ExamId == examId);
        if (q == null) return OpResult.Fail("Question not found.");
        q.ImageData = data; q.ImageType = type;
        db.SaveChanges();
        return OpResult.Success();
    }

    /// <summary>Removes the picture. When <paramref name="stillRequired"/> the question will block launching until a new one is attached.</summary>
    public OpResult ClearImage(int examId, int questionId, bool stillRequired = false)
    {
        using var db = factory.Create();
        if (db.Attempts.Any(x => x.ExamId == examId)) return OpResult.Fail(Locked);
        var q = db.Questions.FirstOrDefault(x => x.Id == questionId && x.ExamId == examId);
        if (q == null) return OpResult.Fail("Question not found.");
        q.ImageData = null; q.ImageType = null; q.ImageRequired = stillRequired;
        db.SaveChanges();
        return OpResult.Success();
    }

    public OpResult MoveQuestion(int examId, int questionId, int direction)
    {
        using var db = factory.Create();
        if (db.Attempts.Any(x => x.ExamId == examId)) return OpResult.Fail(Locked);
        var list = db.Questions.Where(x => x.ExamId == examId).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
        var idx = list.FindIndex(x => x.Id == questionId);
        if (idx < 0) return OpResult.Fail("Question not found.");
        var to = idx + Math.Sign(direction);
        if (to < 0 || to >= list.Count) return OpResult.Success();
        (list[idx], list[to]) = (list[to], list[idx]);
        for (var k = 0; k < list.Count; k++) list[k].SortOrder = k + 1;
        db.SaveChanges();
        return OpResult.Success();
    }

    public OpResult DeleteQuestion(int examId, int questionId)
    {
        using var db = factory.Create();
        if (db.Attempts.Any(x => x.ExamId == examId)) return OpResult.Fail(Locked);
        var q = db.Questions.FirstOrDefault(x => x.Id == questionId && x.ExamId == examId);
        if (q == null) return OpResult.Fail("Question not found.");
        db.Questions.Remove(q);
        db.SaveChanges();
        // An exam with no questions can't stay published.
        if (!db.Questions.Any(x => x.ExamId == examId))
            db.Exams.Where(x => x.Id == examId).ExecuteUpdate(s => s.SetProperty(x => x.IsPublished, false));
        return OpResult.Success();
    }
}

public sealed record QuestionInput(
    QuestionType Type, string? Number, string Text, int Marks,
    string? A = null, string? B = null, string? C = null, string? D = null, string? E = null,
    string? Correct = null, string? ModelAnswer = null, bool ImageRequired = false);

public static class ImageSniffer
{
    /// <summary>Returns the MIME type for PNG/JPEG/GIF/WebP bytes, or null.</summary>
    public static string? Detect(byte[] d)
    {
        if (d.Length >= 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47) return "image/png";
        if (d.Length >= 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF) return "image/jpeg";
        if (d.Length >= 6 && d[0] == 'G' && d[1] == 'I' && d[2] == 'F' && d[3] == '8') return "image/gif";
        if (d.Length >= 12 && d[0] == 'R' && d[1] == 'I' && d[2] == 'F' && d[3] == 'F' && d[8] == 'W' && d[9] == 'E' && d[10] == 'B' && d[11] == 'P') return "image/webp";
        return null;
    }
}
