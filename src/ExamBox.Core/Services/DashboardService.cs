using ExamBox.Data;
using ExamBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Services;

public sealed class DashboardStats
{
    public int Students, ActiveStudents, Exams, PublishedExams, Attempts, Completed;
    public double AvgPercent;
    public List<Attempt> Recent { get; init; } = new();
    public List<User> NewStudents { get; init; } = new();
    /// <summary>Cumulative student count at the end of each of the last 8 weeks (oldest first).</summary>
    public int[] StudentsByWeek { get; init; } = new int[8];
}

public sealed class DashboardService(DbFactory factory)
{
    public DashboardStats Get(int recentCount = 30)
    {
        using var db = factory.Create();
        var done = db.Attempts.Where(a => a.SubmittedAt != null);
        var scores = done.Select(a => new { a.Score, a.TotalMarks }).ToList();
        return new DashboardStats
        {
            Students = db.Users.Count(u => u.Role == UserRole.Student),
            ActiveStudents = db.Users.Count(u => u.Role == UserRole.Student && u.IsActive),
            Exams = db.Exams.Count(),
            PublishedExams = db.Exams.Count(e => e.IsPublished),
            Attempts = db.Attempts.Count(),
            Completed = scores.Count,
            AvgPercent = scores.Count == 0 ? 0 : Math.Round(scores.Average(s => s.TotalMarks == 0 ? 0 : s.Score * 100.0 / s.TotalMarks), 1),
            Recent = done.Include(a => a.Student).Include(a => a.Exam).AsNoTracking()
                .OrderByDescending(a => a.SubmittedAt).Take(recentCount).ToList(),
            NewStudents = db.Users.AsNoTracking().Where(u => u.Role == UserRole.Student)
                .OrderByDescending(u => u.CreatedAt).Take(8).ToList(),
            StudentsByWeek = WeeklyCumulative(db.Users.Where(u => u.Role == UserRole.Student).Select(u => u.CreatedAt).ToList()),
        };
    }

    private static int[] WeeklyCumulative(List<DateTime> created)
    {
        var now = DateTime.UtcNow;
        var result = new int[8];
        for (var i = 0; i < 8; i++)
        {
            var end = now.AddDays(-7 * (7 - i));
            result[i] = created.Count(c => c <= end);
        }
        return result;
    }
}
