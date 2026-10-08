using ExamBox.Data;
using ExamBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Services;

public sealed record OnlineRow(int StudentId, string Name, string Code, string? Class, DateTime LastSeenUtc, string? Exam, bool Paused);

/// <summary>Who has a student portal page open right now. Open pages check in every few seconds.</summary>
public sealed class PresenceService(DbFactory factory)
{
    public const int WindowSeconds = 45;

    public void Touch(int userId)
    {
        using var db = factory.Create();
        db.Users.Where(u => u.Id == userId).ExecuteUpdate(s => s.SetProperty(u => u.LastSeenAt, DateTime.UtcNow));
    }

    public List<OnlineRow> Online()
    {
        using var db = factory.Create();
        var cut = DateTime.UtcNow.AddSeconds(-WindowSeconds);
        var users = db.Users.AsNoTracking().Where(u => u.Role == UserRole.Student && u.LastSeenAt != null && u.LastSeenAt > cut)
            .OrderBy(u => u.FullName).ToList();
        var ids = users.Select(u => u.Id).ToList();
        var running = db.Attempts.AsNoTracking().Include(a => a.Exam).Where(a => ids.Contains(a.StudentId) && a.SubmittedAt == null).ToList();
        return users.Select(u =>
        {
            var a = running.FirstOrDefault(x => x.StudentId == u.Id);
            return new OnlineRow(u.Id, u.FullName, u.Username, u.Department, u.LastSeenAt!.Value, a?.Exam?.Title, a?.PausedAt != null);
        }).ToList();
    }

    public int OnlineCount()
    {
        using var db = factory.Create();
        var cut = DateTime.UtcNow.AddSeconds(-WindowSeconds);
        return db.Users.Count(u => u.Role == UserRole.Student && u.LastSeenAt != null && u.LastSeenAt > cut);
    }
}
