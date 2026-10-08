using Microsoft.EntityFrameworkCore;

namespace ExamBox.Data;

/// <summary>
/// Creates a fresh database, or upgrades an older one in place without losing data.
/// The schema version lives in SQLite's <c>PRAGMA user_version</c>.
/// </summary>
public static class SchemaUpgrader
{
    public const int Current = 7;

    public static void Run(AppDb db)
    {
        var conn = db.Database.GetDbConnection();
        conn.Open();
        try
        {
            var existing = Scalar(conn, "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Users'") > 0;
            if (!existing)
            {
                db.Database.EnsureCreated();
                Exec(conn, $"PRAGMA user_version = {Current}");
                return;
            }
            var v = Scalar(conn, "PRAGMA user_version");
            if (v >= Current) return;

            using var tx = conn.BeginTransaction();
            if (v < 2) ToV2(conn, tx);
            // v3: temporary passwords no longer exist; nobody is forced to change a password.
            if (v < 3) Exec(conn, "UPDATE Users SET MustChangePassword = 0", tx);
            if (v < 4) Exec(conn, "ALTER TABLE Users ADD COLUMN PasswordCipher TEXT NULL", tx);
            if (v < 7)
            {
                Exec(conn, "ALTER TABLE Users ADD COLUMN LastSeenAt TEXT NULL", tx);
                Exec(conn, "CREATE TABLE IF NOT EXISTS Classes (Id INTEGER NOT NULL CONSTRAINT PK_Classes PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL COLLATE NOCASE, CreatedAt TEXT NOT NULL)", tx);
                Exec(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_Classes_Name ON Classes (Name)", tx);
                Exec(conn, "CREATE TABLE IF NOT EXISTS Activity (Id INTEGER NOT NULL CONSTRAINT PK_Activity PRIMARY KEY AUTOINCREMENT, At TEXT NOT NULL, Kind TEXT NOT NULL, Actor TEXT NULL, Subject TEXT NULL, Details TEXT NULL)", tx);
                Exec(conn, "CREATE INDEX IF NOT EXISTS IX_Activity_At ON Activity (At)", tx);
                // classes that students and exams already use become real classes
                Exec(conn, "INSERT OR IGNORE INTO Classes (Name, CreatedAt) SELECT DISTINCT TRIM(Department), datetime('now') FROM Users WHERE Role = 'Student' AND Department IS NOT NULL AND TRIM(Department) <> ''", tx);
                Exec(conn, "INSERT OR IGNORE INTO Classes (Name, CreatedAt) SELECT DISTINCT TRIM(ForDepartment), datetime('now') FROM Exams WHERE ForDepartment IS NOT NULL AND TRIM(ForDepartment) <> ''", tx);
            }
            if (v < 6)
            {
                Exec(conn, "ALTER TABLE Attempts ADD COLUMN PausedAt TEXT NULL", tx);
                Exec(conn, "ALTER TABLE Attempts ADD COLUMN PausedSeconds INTEGER NOT NULL DEFAULT 0", tx);
                Exec(conn, "ALTER TABLE Attempts ADD COLUMN TimeAdjustSeconds INTEGER NOT NULL DEFAULT 0", tx);
            }
            if (v < 5) Exec(conn, "ALTER TABLE Users ADD COLUMN SessionVersion INTEGER NOT NULL DEFAULT 0", tx);
            Exec(conn, $"PRAGMA user_version = {Current}", tx);
            tx.Commit();
        }
        finally { conn.Close(); }
    }

    static void ToV2(System.Data.Common.DbConnection c, System.Data.Common.DbTransaction tx)
    {
        string[] sql =
        {
            "ALTER TABLE Exams ADD COLUMN OpensAt TEXT NULL",
            "ALTER TABLE Exams ADD COLUMN ClosesAt TEXT NULL",
            "ALTER TABLE Exams ADD COLUMN ForDepartment TEXT NULL",
            "ALTER TABLE Exams ADD COLUMN ShuffleQuestions INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE Exams ADD COLUMN ShowCorrectAnswers INTEGER NOT NULL DEFAULT 1",
            "ALTER TABLE Questions ADD COLUMN Type TEXT NOT NULL DEFAULT 'Objective'",
            "ALTER TABLE Questions ADD COLUMN Number TEXT NULL",
            "ALTER TABLE Questions ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE Questions ADD COLUMN OptionE TEXT NULL",
            "ALTER TABLE Questions ADD COLUMN ModelAnswer TEXT NULL",
            "ALTER TABLE Questions ADD COLUMN ImageData BLOB NULL",
            "ALTER TABLE Questions ADD COLUMN ImageType TEXT NULL",
            "ALTER TABLE Questions ADD COLUMN ImageRequired INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE Attempts ADD COLUMN ObjectiveScore INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE Attempts ADD COLUMN TheoryScore INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE Attempts ADD COLUMN PendingMarking INTEGER NOT NULL DEFAULT 0",
            "ALTER TABLE Attempts ADD COLUMN MarkedAt TEXT NULL",
            "ALTER TABLE Answers ADD COLUMN Text TEXT NULL",
            "ALTER TABLE Answers ADD COLUMN Marks INTEGER NULL",
            "ALTER TABLE Answers ADD COLUMN Comment TEXT NULL",
            "UPDATE Questions SET SortOrder = Id",
            "UPDATE Attempts SET ObjectiveScore = Score",
        };
        foreach (var s in sql) Exec(c, s, tx);
    }

    static long Scalar(System.Data.Common.DbConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    static void Exec(System.Data.Common.DbConnection c, string sql, System.Data.Common.DbTransaction? tx = null)
    {
        using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
