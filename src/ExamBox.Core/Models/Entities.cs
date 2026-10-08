using System.ComponentModel.DataAnnotations;

namespace ExamBox.Models;

public enum UserRole { Admin, Student }

public class User
{
    public int Id { get; set; }
    [Required, StringLength(120)] public string FullName { get; set; } = "";
    /// <summary>Login ID: student/matric number for students, username for staff.</summary>
    [Required, StringLength(60)] public string Username { get; set; } = "";
    [StringLength(160)] public string? Email { get; set; }
    [StringLength(80)] public string? Department { get; set; }
    public string PasswordHash { get; set; } = "";
    /// <summary>Student passwords only: the password the admin set, encrypted, so the student and the admin can look it up. Null when unknown.</summary>
    public string? PasswordCipher { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    /// <summary>Bumped by the admin to end every open sign-in of this student ("force sign-out").</summary>
    public int SessionVersion { get; set; }
    public int FailedLogins { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public List<Attempt> Attempts { get; set; } = new();
}

public enum QuestionType { Objective, Theory }

public enum ExamState { Draft, Scheduled, Open, Closed }

public class Exam
{
    public int Id { get; set; }
    [Required, StringLength(160)] public string Title { get; set; } = "";
    [StringLength(1000)] public string? Description { get; set; }
    [Range(1, 600)] public int DurationMinutes { get; set; } = 30;
    [Range(1, 100)] public int PassMarkPercent { get; set; } = 50;
    /// <summary>True once the exam has been launched (a draft is a reusable template).</summary>
    public bool IsPublished { get; set; }
    /// <summary>When students may start. Null = as soon as it is launched.</summary>
    public DateTime? OpensAt { get; set; }
    /// <summary>When the exam shuts for everyone. Null = stays open until closed by hand.</summary>
    public DateTime? ClosesAt { get; set; }
    /// <summary>Restrict to one class/department (matches User.Department). Null = everyone.</summary>
    [StringLength(80)] public string? ForDepartment { get; set; }
    public bool ShuffleQuestions { get; set; }
    public bool ShowCorrectAnswers { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Question> Questions { get; set; } = new();
    public List<Attempt> Attempts { get; set; } = new();

    public ExamState StateAt(DateTime utcNow)
    {
        if (!IsPublished) return ExamState.Draft;
        if (ClosesAt is { } c && utcNow >= c) return ExamState.Closed;
        if (OpensAt is { } o && utcNow < o) return ExamState.Scheduled;
        return ExamState.Open;
    }
}

public class Question
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public Exam? Exam { get; set; }
    public QuestionType Type { get; set; } = QuestionType.Objective;
    /// <summary>Display label such as "1", "2" or "3a". Blank = automatic.</summary>
    [StringLength(12)] public string? Number { get; set; }
    public int SortOrder { get; set; }
    [Required, StringLength(4000)] public string Text { get; set; } = "";
    [StringLength(500)] public string OptionA { get; set; } = "";
    [StringLength(500)] public string OptionB { get; set; } = "";
    [StringLength(500)] public string? OptionC { get; set; }
    [StringLength(500)] public string? OptionD { get; set; }
    [StringLength(500)] public string? OptionE { get; set; }
    /// <summary>'A'..'E' for objective questions; empty for theory.</summary>
    [RegularExpression("[A-E]?")] public string? CorrectOption { get; set; } = "A";
    [Range(0, 100)] public int Marks { get; set; } = 1;
    /// <summary>Marking guide shown to the teacher (and optionally the student) for theory questions.</summary>
    [StringLength(4000)] public string? ModelAnswer { get; set; }
    public byte[]? ImageData { get; set; }
    [StringLength(60)] public string? ImageType { get; set; }
    /// <summary>The import said this question needs a picture; the exam cannot launch until it is attached.</summary>
    public bool ImageRequired { get; set; }

    public bool HasImage => ImageData is { Length: > 0 };
    public bool MissingImage => ImageRequired && !HasImage;

    public IEnumerable<(string Key, string Text)> Options()
    {
        if (Type != QuestionType.Objective) yield break;
        yield return ("A", OptionA);
        yield return ("B", OptionB);
        if (!string.IsNullOrWhiteSpace(OptionC)) yield return ("C", OptionC!);
        if (!string.IsNullOrWhiteSpace(OptionD)) yield return ("D", OptionD!);
        if (!string.IsNullOrWhiteSpace(OptionE)) yield return ("E", OptionE!);
    }
}

public class Attempt
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public Exam? Exam { get; set; }
    public int StudentId { get; set; }
    public User? Student { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    /// <summary>Total score (objective + marked theory).</summary>
    public int Score { get; set; }
    public int ObjectiveScore { get; set; }
    public int TheoryScore { get; set; }
    public int TotalMarks { get; set; }
    /// <summary>Submitted, but theory answers still need a teacher's marks.</summary>
    public bool PendingMarking { get; set; }
    public DateTime? MarkedAt { get; set; }
    public List<Answer> Answers { get; set; } = new();
    public double Percent => TotalMarks == 0 ? 0 : Math.Round(Score * 100.0 / TotalMarks, 1);
    public bool IsFinal => SubmittedAt != null && !PendingMarking;
}

public class Answer
{
    public int Id { get; set; }
    public int AttemptId { get; set; }
    public Attempt? Attempt { get; set; }
    public int QuestionId { get; set; }
    public Question? Question { get; set; }
    public string? Selected { get; set; }
    /// <summary>Typed answer for theory questions.</summary>
    public string? Text { get; set; }
    public int? Marks { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
}
