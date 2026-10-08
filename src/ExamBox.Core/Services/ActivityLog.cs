using ExamBox.Data;
using ExamBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Services;

/// <summary>The activity log: who signed in, which exam started, what the admin changed.</summary>
public sealed class ActivityLog(DbFactory factory)
{
    public const int Keep = 20000;

    public static void Write(AppDb db, string kind, string? actor, string? subject, string? details = null)
    {
        db.Activity.Add(new ActivityEntry { Kind = kind, Actor = Trunc(actor, 120), Subject = Trunc(subject, 160), Details = Trunc(details, 400) });
        db.SaveChanges();
    }

    public void Write(string kind, string? actor, string? subject, string? details = null)
    {
        try { using var db = factory.Create(); Write(db, kind, actor, subject, details); }
        catch { /* logging must never break the action being logged */ }
    }

    private static string? Trunc(string? s, int n) => s == null ? null : s.Length <= n ? s : s[..n];

    public List<ActivityEntry> List(string? kind = null, string? search = null, int take = 500)
    {
        using var db = factory.Create();
        var q = db.Activity.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(kind)) q = q.Where(a => a.Kind == kind);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(a => EF.Functions.Like(a.Actor ?? "", $"%{s}%") || EF.Functions.Like(a.Subject ?? "", $"%{s}%") || EF.Functions.Like(a.Details ?? "", $"%{s}%"));
        }
        return q.OrderByDescending(a => a.At).ThenByDescending(a => a.Id).Take(take).ToList();
    }

    public sealed record Today(int Events, int SignIns, int Failed, int AdminActions);

    /// <summary>Counts since local midnight, for the page header.</summary>
    public Today CountToday()
    {
        using var db = factory.Create();
        var since = DateTime.Now.Date.ToUniversalTime();
        var g = db.Activity.AsNoTracking().Where(a => a.At >= since).GroupBy(a => a.Kind).Select(x => new { x.Key, N = x.Count() }).ToList();
        int N(string k) => g.FirstOrDefault(x => x.Key == k)?.N ?? 0;
        return new Today(g.Sum(x => x.N), N("signin"), N("failed"), N("admin"));
    }

    /// <summary>Keeps the table from growing without limit.</summary>
    public void Trim()
    {
        try
        {
            using var db = factory.Create();
            var cut = db.Activity.OrderByDescending(a => a.Id).Skip(Keep).Select(a => (int?)a.Id).FirstOrDefault();
            if (cut != null) db.Activity.Where(a => a.Id <= cut).ExecuteDelete();
        }
        catch { }
    }
}
