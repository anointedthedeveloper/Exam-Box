using ExamBox.Data;
using ExamBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Services;

public sealed record LiveRow(int AttemptId, int StudentId, string Student, string StudentCode, DateTime StartedUtc, int SecondsLeft, bool Paused);

/// <summary>What an admin can do to an exam a student is currently sitting.</summary>
public sealed class LiveService(DbFactory factory)
{
    public List<LiveRow> List(int examId)
    {
        using var db = factory.Create();
        var list = db.Attempts.Include(a => a.Student).Include(a => a.Exam)
            .Where(a => a.ExamId == examId && a.SubmittedAt == null).OrderBy(a => a.StartedAt).ToList();
        return list.Select(a => new LiveRow(a.Id, a.StudentId, a.Student!.FullName, a.Student.Username, a.StartedAt,
            Math.Max(0, AttemptService.SecondsLeft(a, a.Exam!)), a.PausedAt != null)).ToList();
    }

    /// <summary>Freezes the clock and signs the student out. When they sign back in the exam is waiting with the time left.</summary>
    public OpResult Pause(int attemptId)
    {
        using var db = factory.Create();
        var a = db.Attempts.FirstOrDefault(x => x.Id == attemptId && x.SubmittedAt == null);
        if (a == null) return OpResult.Fail("This exam is not running.");
        if (a.PausedAt == null) a.PausedAt = DateTime.UtcNow;
        db.Users.Where(u => u.Id == a.StudentId).ExecuteUpdate(s => s.SetProperty(u => u.SessionVersion, u => u.SessionVersion + 1));
        db.SaveChanges();
        return OpResult.Success();
    }

    /// <summary>Lets a paused exam run again from the admin side.</summary>
    public OpResult Resume(int attemptId)
    {
        using var db = factory.Create();
        var a = db.Attempts.FirstOrDefault(x => x.Id == attemptId && x.SubmittedAt == null);
        if (a == null) return OpResult.Fail("This exam is not running.");
        if (a.PausedAt != null) { a.PausedSeconds += (int)(DateTime.UtcNow - a.PausedAt.Value).TotalSeconds; a.PausedAt = null; db.SaveChanges(); }
        return OpResult.Success();
    }

    /// <summary>Sets how many minutes this student has left from now (works while running or paused).</summary>
    public OpResult SetMinutesLeft(int attemptId, int minutes)
    {
        if (minutes is < 0 or > 1440) return OpResult.Fail("Enter between 0 and 1440 minutes.");
        using var db = factory.Create();
        var a = db.Attempts.Include(x => x.Exam).FirstOrDefault(x => x.Id == attemptId && x.SubmittedAt == null);
        if (a == null) return OpResult.Fail("This exam is not running.");
        var now = DateTime.UtcNow;
        var current = (int)(AttemptService.Deadline(a, a.Exam!, now) - now).TotalSeconds;
        // a paused attempt keeps its deadline moving with the clock, so compare against the frozen remainder instead
        if (a.PausedAt != null) current = (int)(AttemptService.Deadline(a, a.Exam!, a.PausedAt.Value) - a.PausedAt.Value).TotalSeconds;
        a.TimeAdjustSeconds += minutes * 60 - current;
        db.SaveChanges();
        return OpResult.Success();
    }
}
