using Microsoft.EntityFrameworkCore;

namespace ExamBox.Data;

/// <summary>
/// Creates a fresh database, or upgrades an older one in place without losing data.
/// The schema version lives in SQLite's <c>PRAGMA user_version</c>.
/// </summary>
public static class SchemaUpgrader
{
    public const int Current = 2;

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
