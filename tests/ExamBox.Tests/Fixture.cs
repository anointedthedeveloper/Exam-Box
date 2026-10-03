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
