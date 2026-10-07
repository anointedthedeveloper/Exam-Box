using ExamBox.Data;
using ExamBox.Services;

namespace ExamBox.Tests;

/// <summary>A throwaway database in a temp folder.</summary>
public sealed class TempDb : IDisposable
{
    public string Dir { get; } = Path.Combine(Path.GetTempPath(), "exambox-test-" + Guid.NewGuid().ToString("N"));
    public DbFactory Factory { get; }
    public AuthService Auth { get; }
    public StudentService Students { get; }
    public ExamService Exams { get; }

    public TempDb()
    {
        Factory = new DbFactory(Dir);
        Factory.Initialize();
        Auth = new AuthService(Factory);
        Students = new StudentService(Factory);
        Exams = new ExamService(Factory);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(Dir, true); } catch { /* best effort */ }
    }
}

public static class TestExt
{
    public static OpResult SaveQuestion(this ExamService s, int examId, int qid, string text, string a, string b, string? c, string? d, string correct, int marks) =>
        s.SaveQuestion(examId, qid, new QuestionInput(ExamBox.Models.QuestionType.Objective, null, text, marks, a, b, c, d, null, correct));

    public static OpResult Theory(this ExamService s, int examId, string text, int marks, string? number = null, string? model = null) =>
        s.SaveQuestion(examId, 0, new QuestionInput(ExamBox.Models.QuestionType.Theory, number, text, marks, ModelAnswer: model));

    public static OpResult SetPublished(this ExamService s, int id, bool on) =>
        on ? s.Launch(id, null, null, null) : s.Unpublish(id);
}
