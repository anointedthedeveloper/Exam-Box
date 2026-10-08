using System.ComponentModel.DataAnnotations;
using ExamBox.Services;

namespace ExamBox.Models;

public class LoginVm
{
    [Required(ErrorMessage = "Enter your student ID.")] public string Identifier { get; set; } = "";
    [Required(ErrorMessage = "Enter your password.")] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}

public class TakeExamVm
{
    public Attempt Attempt { get; set; } = null!;
    public Exam Exam { get; set; } = null!;
    public List<Question> Questions { get; set; } = new();
    public Dictionary<int, Answer> Saved { get; set; } = new();
    public int SecondsLeft { get; set; }
}

public static class QLabel
{
    /// <summary>Display numbers: the teacher's own label when given, otherwise 1, 2, 3 ... counting only unlabelled questions.</summary>
    public static Dictionary<int, string> Build(IEnumerable<Question> questions)
    {
        var map = new Dictionary<int, string>(); var n = 0;
        foreach (var q in questions)
            map[q.Id] = string.IsNullOrWhiteSpace(q.Number) ? (++n).ToString() : q.Number!.Trim();
        return map;
    }

    /// <summary>A theory row with no marks is only reading material (e.g. a passage).</summary>
    public static bool IsPassage(Question q) => q.Type == QuestionType.Theory && q.Marks == 0;
}
