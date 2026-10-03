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
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public int FailedLogins { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public List<Attempt> Attempts { get; set; } = new();
}

public class Exam
{
    public int Id { get; set; }
    [Required, StringLength(160)] public string Title { get; set; } = "";
    [StringLength(1000)] public string? Description { get; set; }
    [Range(1, 600)] public int DurationMinutes { get; set; } = 30;
    [Range(1, 100)] public int PassMarkPercent { get; set; } = 50;
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<Question> Questions { get; set; } = new();
    public List<Attempt> Attempts { get; set; } = new();
}

public class Question
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public Exam? Exam { get; set; }
    [Required, StringLength(2000)] public string Text { get; set; } = "";
    [Required, StringLength(500)] public string OptionA { get; set; } = "";
    [Required, StringLength(500)] public string OptionB { get; set; } = "";
    [StringLength(500)] public string? OptionC { get; set; }
    [StringLength(500)] public string? OptionD { get; set; }
    /// <summary>'A'..'D'</summary>
    [Required, RegularExpression("[A-D]")] public string CorrectOption { get; set; } = "A";
    [Range(1, 100)] public int Marks { get; set; } = 1;

    public IEnumerable<(string Key, string Text)> Options()
    {
        yield return ("A", OptionA);
        yield return ("B", OptionB);
        if (!string.IsNullOrWhiteSpace(OptionC)) yield return ("C", OptionC!);
        if (!string.IsNullOrWhiteSpace(OptionD)) yield return ("D", OptionD!);
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
    public int Score { get; set; }
    public int TotalMarks { get; set; }
    public List<Answer> Answers { get; set; } = new();
    public double Percent => TotalMarks == 0 ? 0 : Math.Round(Score * 100.0 / TotalMarks, 1);
}

public class Answer
{
    public int Id { get; set; }
    public int AttemptId { get; set; }
    public Attempt? Attempt { get; set; }
    public int QuestionId { get; set; }
    public Question? Question { get; set; }
    public string? Selected { get; set; }
}
