using Microsoft.EntityFrameworkCore;

namespace ExamBox.Data;

/// <summary>Locates the ExamBox database and hands out short-lived contexts.</summary>
public sealed class DbFactory
{
    public string DataDir { get; }
    public string DbPath { get; }
    public string ConnectionString => $"Data Source={DbPath}";

    /// <param name="dataDir">Folder for the database and settings. Defaults to %LOCALAPPDATA%\ExamBox
    /// (override with the EXAMBOX_DATA environment variable).</param>
    public DbFactory(string? dataDir = null)
    {
        DataDir = dataDir
                  ?? Environment.GetEnvironmentVariable("EXAMBOX_DATA")
                  ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExamBox");
        Directory.CreateDirectory(DataDir);
        DbPath = Path.Combine(DataDir, "exambox.db");
    }

    public AppDb Create() =>
        new(new DbContextOptionsBuilder<AppDb>().UseSqlite(ConnectionString).Options);

    /// <summary>Writes a consistent copy of the database to <paramref name="targetPath"/> (replaced if it exists).</summary>
    public void BackupTo(string targetPath)
    {
        var full = Path.GetFullPath(targetPath);
        if (string.Equals(full, Path.GetFullPath(DbPath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose a different file than the live database.");
        if (File.Exists(full)) File.Delete(full);
        using var db = Create();
        // VACUUM INTO produces a transactionally consistent copy even while the portal is writing.
        db.Database.ExecuteSqlRaw("VACUUM INTO '" + full.Replace("'", "''") + "'");
    }

    /// <summary>Creates the schema on first run.</summary>
    public void Initialize()
    {
        using var db = Create();
        db.Database.EnsureCreated();
    }
}
