using System.ComponentModel.DataAnnotations;

namespace ExamBox.Models;

public class LoginVm
{
    [Required(ErrorMessage = "Enter your student ID.")] public string Identifier { get; set; } = "";
    [Required(ErrorMessage = "Enter your password.")] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}

public class ChangePasswordVm
{
    [Required, DataType(DataType.Password)] public string CurrentPassword { get; set; } = "";
    [Required, DataType(DataType.Password)] public string NewPassword { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
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
