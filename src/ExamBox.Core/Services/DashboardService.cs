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
}

public sealed class DashboardService(DbFactory factory)
{
    public DashboardStats Get()
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
                .OrderByDescending(a => a.SubmittedAt).Take(8).ToList(),
            NewStudents = db.Users.AsNoTracking().Where(u => u.Role == UserRole.Student)
                .OrderByDescending(u => u.CreatedAt).Take(6).ToList(),
        };
    }
}
