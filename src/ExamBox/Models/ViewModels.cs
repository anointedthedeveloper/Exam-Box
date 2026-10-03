using System.ComponentModel.DataAnnotations;

namespace ExamBox.Models;

public class LoginVm
{
    [Required(ErrorMessage = "Enter your ID or email.")] public string Identifier { get; set; } = "";
    [Required(ErrorMessage = "Enter your password.")] public string Password { get; set; } = "";
    public string Portal { get; set; } = "student";
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public class SetupVm
{
    [Required, StringLength(120)] public string FullName { get; set; } = "";
    [Required, StringLength(60)] public string Username { get; set; } = "";
    [EmailAddress, StringLength(160)] public string? Email { get; set; }
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

public class ChangePasswordVm
{
    [Required, DataType(DataType.Password)] public string CurrentPassword { get; set; } = "";
    [Required, DataType(DataType.Password)] public string NewPassword { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

public class StudentFormVm
{
    public int? Id { get; set; }
    [Required, StringLength(120), Display(Name = "Full name")] public string FullName { get; set; } = "";
    [Required, StringLength(60), Display(Name = "Student ID")] public string Username { get; set; } = "";
    [EmailAddress, StringLength(160)] public string? Email { get; set; }
    [StringLength(80), Display(Name = "Department / class")] public string? Department { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AdminDashboardVm
{
    public int Students, ActiveStudents, Exams, PublishedExams, Attempts, Completed;
    public double AvgPercent;
    public List<Attempt> Recent { get; set; } = new();
    public List<User> NewStudents { get; set; } = new();
}

public class StudentDashboardVm
{
    public User Student { get; set; } = null!;
    public List<Exam> Available { get; set; } = new();
    public List<Attempt> InProgress { get; set; } = new();
    public List<Attempt> Completed { get; set; } = new();
}

public class TakeExamVm
{
    public Attempt Attempt { get; set; } = null!;
    public Exam Exam { get; set; } = null!;
    public List<Question> Questions { get; set; } = new();
    public int SecondsLeft { get; set; }
}
