using ExamBox.Data;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Services;

public sealed record ExamStat(int ExamId, string Title, int Attempts, double AvgPercent, double PassRate, double HighPercent, double LowPercent);
public sealed record MonthPoint(DateTime Month, int Passed, int Failed)
{
    public int Total => Passed + Failed;
}
public sealed record StudentStat(int StudentId, string Name, string Username, int Exams, double AvgPercent);
public sealed record ResultRow(string Student, string StudentId, string Exam, int Score, int TotalMarks, double Percent, bool Passed, DateTime SubmittedUtc);

public sealed class Report
{
    public int Attempts;
    public double AvgPercent, PassRate, HighPercent, LowPercent;
    /// <summary>Counts for grades A (90+), B (80+), C (70+), D (60+), E (below 60).</summary>
    public int[] GradeCounts { get; init; } = new int[5];
    /// <summary>Passed/failed per calendar month for the last 6 months (oldest first, zero-filled).</summary>
    public List<MonthPoint> Trend { get; init; } = new();
    public List<ExamStat> Exams { get; init; } = new();
    public List<StudentStat> TopStudents { get; init; } = new();
}

/// <summary>Aggregates submitted attempts for the dashboard and the Reports page.</summary>
public sealed class ReportService(DbFactory factory)
{
    public static readonly string[] GradeLabels = { "A", "B", "C", "D", "E" };

    public static int GradeIndex(double percent) => percent >= 90 ? 0 : percent >= 80 ? 1 : percent >= 70 ? 2 : percent >= 60 ? 3 : 4;

    // Keep ids alongside rows for grouping (ResultRow is the public/export shape).
    private sealed record Raw(ResultRow Row, int ExamId, int StudentId);

    private List<Raw> LoadRaw(int? examId)
    {
        using var db = factory.Create();
        var q = db.Attempts.AsNoTracking().Where(a => a.SubmittedAt != null);
        if (examId != null) q = q.Where(a => a.ExamId == examId);
        return q.Select(a => new
        {
            Student = a.Student!.FullName, Id = a.Student.Username, Exam = a.Exam!.Title, a.Score, a.TotalMarks,
            Submitted = a.SubmittedAt!.Value, Pass = a.Exam.PassMarkPercent, a.ExamId, a.StudentId,
        }).AsEnumerable().Select(a =>
        {
            var pct = a.TotalMarks == 0 ? 0 : a.Score * 100.0 / a.TotalMarks;
            return new Raw(new ResultRow(a.Student, a.Id, a.Exam, a.Score, a.TotalMarks, Math.Round(pct, 1), pct >= a.Pass, a.Submitted), a.ExamId, a.StudentId);
        }).ToList();
    }

    /// <param name="examId">Limit to one exam (null = all).</param>
    /// <param name="sinceUtc">Only count submissions from this moment on (null = all time). The monthly trend ignores this.</param>
    public Report Build(int? examId = null, DateTime? sinceUtc = null)
    {
        var all = LoadRaw(examId);
        var rows = sinceUtc == null ? all : all.Where(r => r.Row.SubmittedUtc >= sinceUtc).ToList();

        var grades = new int[5];
        foreach (var r in rows) grades[GradeIndex(r.Row.Percent)]++;

        var now = DateTime.UtcNow;
        var first = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        var trend = Enumerable.Range(0, 6).Select(i =>
        {
            var m = first.AddMonths(i);
            var inMonth = all.Where(r => r.Row.SubmittedUtc.Year == m.Year && r.Row.SubmittedUtc.Month == m.Month).ToList();
            return new MonthPoint(m, inMonth.Count(r => r.Row.Passed), inMonth.Count(r => !r.Row.Passed));
        }).ToList();

        return new Report
        {
            Attempts = rows.Count,
            AvgPercent = rows.Count == 0 ? 0 : Math.Round(rows.Average(r => r.Row.Percent), 1),
            PassRate = rows.Count == 0 ? 0 : Math.Round(rows.Count(r => r.Row.Passed) * 100.0 / rows.Count, 1),
            HighPercent = rows.Count == 0 ? 0 : rows.Max(r => r.Row.Percent),
            LowPercent = rows.Count == 0 ? 0 : rows.Min(r => r.Row.Percent),
            GradeCounts = grades,
            Trend = trend,
            Exams = rows.GroupBy(r => r.ExamId).Select(g => new ExamStat(g.Key, g.First().Row.Exam, g.Count(),
                    Math.Round(g.Average(r => r.Row.Percent), 1), Math.Round(g.Count(r => r.Row.Passed) * 100.0 / g.Count(), 1),
                    g.Max(r => r.Row.Percent), g.Min(r => r.Row.Percent)))
                .OrderByDescending(e => e.Attempts).ThenBy(e => e.Title).ToList(),
            TopStudents = rows.GroupBy(r => r.StudentId).Select(g => new StudentStat(g.Key, g.First().Row.Student, g.First().Row.StudentId,
                    g.Count(), Math.Round(g.Average(r => r.Row.Percent), 1)))
                .OrderByDescending(s => s.AvgPercent).ThenByDescending(s => s.Exams).Take(10).ToList(),
        };
    }

    /// <summary>Every submitted result matching the filters, newest first (for CSV export).</summary>
    public List<ResultRow> Export(int? examId = null, DateTime? sinceUtc = null) =>
        LoadRaw(examId).Select(r => r.Row).Where(r => sinceUtc == null || r.SubmittedUtc >= sinceUtc)
            .OrderByDescending(r => r.SubmittedUtc).ToList();
}
