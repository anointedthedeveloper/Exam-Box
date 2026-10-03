using ExamBox.Data;
using ExamBox.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Controllers;

[Authorize(Roles = "Admin"), Route("admin")]
public class DashboardController(AppDb db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var done = db.Attempts.Where(a => a.SubmittedAt != null);
        var vm = new AdminDashboardVm
        {
            Students = await db.Users.CountAsync(u => u.Role == UserRole.Student),
            ActiveStudents = await db.Users.CountAsync(u => u.Role == UserRole.Student && u.IsActive),
            Exams = await db.Exams.CountAsync(),
            PublishedExams = await db.Exams.CountAsync(e => e.IsPublished),
            Attempts = await db.Attempts.CountAsync(),
            Completed = await done.CountAsync(),
            Recent = await done.Include(a => a.Student).Include(a => a.Exam)
                .OrderByDescending(a => a.SubmittedAt).Take(8).ToListAsync(),
            NewStudents = await db.Users.Where(u => u.Role == UserRole.Student)
                .OrderByDescending(u => u.CreatedAt).Take(6).ToListAsync(),
        };
        // SQLite can't average computed ratios server-side reliably; do it in memory over submitted attempts.
        var scores = await done.Select(a => new { a.Score, a.TotalMarks }).ToListAsync();
        vm.AvgPercent = scores.Count == 0 ? 0 : Math.Round(scores.Average(s => s.TotalMarks == 0 ? 0 : s.Score * 100.0 / s.TotalMarks), 1);
        return View(vm);
    }
}
