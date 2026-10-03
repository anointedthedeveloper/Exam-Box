using ExamBox.Data;
using ExamBox.Models;
using ExamBox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Controllers;

[Authorize(Roles = "Student"), Route("portal")]
public class PortalController(AppDb db) : Controller
{
    private const int GraceSeconds = 30;
    private int Me => User.GetUserId()!.Value;

    private static DateTime Deadline(Attempt a, Exam e) => a.StartedAt.AddMinutes(e.DurationMinutes);

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        await AutoSubmitExpired();
        var student = await db.Users.AsNoTracking().FirstAsync(u => u.Id == Me);
        var attempts = await db.Attempts.AsNoTracking().Include(a => a.Exam).Where(a => a.StudentId == Me)
            .OrderByDescending(a => a.StartedAt).ToListAsync();
        var takenIds = attempts.Select(a => a.ExamId).ToList();
        var vm = new StudentDashboardVm
        {
            Student = student,
            InProgress = attempts.Where(a => a.SubmittedAt == null).ToList(),
            Completed = attempts.Where(a => a.SubmittedAt != null).ToList(),
            Available = await db.Exams.AsNoTracking().Include(e => e.Questions)
                .Where(e => e.IsPublished && !takenIds.Contains(e.Id)).OrderBy(e => e.Title).ToListAsync(),
        };
        return View(vm);
    }

    [HttpPost("start/{examId:int}")]
    public async Task<IActionResult> Start(int examId)
    {
        var exam = await db.Exams.Include(e => e.Questions).FirstOrDefaultAsync(e => e.Id == examId && e.IsPublished);
        if (exam == null || exam.Questions.Count == 0) return NotFound();
        var existing = await db.Attempts.FirstOrDefaultAsync(a => a.ExamId == examId && a.StudentId == Me);
        if (existing != null) return RedirectToAction(existing.SubmittedAt == null ? nameof(Take) : nameof(Result), new { id = existing.Id });
        var attempt = new Attempt { ExamId = examId, StudentId = Me, StartedAt = DateTime.UtcNow, TotalMarks = exam.Questions.Sum(q => q.Marks) };
        db.Attempts.Add(attempt);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) // double-click race on the unique (exam, student) index
        {
            db.ChangeTracker.Clear();
            attempt = await db.Attempts.FirstAsync(a => a.ExamId == examId && a.StudentId == Me);
        }
        return RedirectToAction(nameof(Take), new { id = attempt.Id });
    }

    [HttpGet("exam/{id:int}")]
    public async Task<IActionResult> Take(int id)
    {
        var a = await db.Attempts.Include(x => x.Exam).ThenInclude(e => e!.Questions).Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.Id == id && x.StudentId == Me);
        if (a == null) return NotFound();
        if (a.SubmittedAt != null) return RedirectToAction(nameof(Result), new { id });
        var left = (int)(Deadline(a, a.Exam!) - DateTime.UtcNow).TotalSeconds;
        if (left <= 0) { await Grade(a, new Dictionary<string, string>()); return RedirectToAction(nameof(Result), new { id }); }
        return View(new TakeExamVm { Attempt = a, Exam = a.Exam!, Questions = a.Exam!.Questions.OrderBy(q => q.Id).ToList(), SecondsLeft = left });
    }

    [HttpPost("exam/{id:int}")]
    public async Task<IActionResult> Submit(int id)
    {
        var a = await db.Attempts.Include(x => x.Exam).ThenInclude(e => e!.Questions)
            .FirstOrDefaultAsync(x => x.Id == id && x.StudentId == Me);
        if (a == null) return NotFound();
        if (a.SubmittedAt != null) return RedirectToAction(nameof(Result), new { id });
        // Server-side time check: late submissions beyond the grace window record no answers.
        var late = DateTime.UtcNow > Deadline(a, a.Exam!).AddSeconds(GraceSeconds);
        var picked = new Dictionary<string, string>();
        if (!late)
            foreach (var q in a.Exam!.Questions)
                if (Request.Form.TryGetValue($"q_{q.Id}", out var v) && v.Count > 0) picked[q.Id.ToString()] = v[0]!;
        await Grade(a, picked);
        TempData["Success"] = late ? "Time was up — your exam was submitted automatically." : "Exam submitted.";
        return RedirectToAction(nameof(Result), new { id });
    }

    [HttpGet("result/{id:int}")]
    public async Task<IActionResult> Result(int id)
    {
        var a = await db.Attempts.Include(x => x.Exam).ThenInclude(e => e!.Questions).Include(x => x.Answers)
            .FirstOrDefaultAsync(x => x.Id == id && x.StudentId == Me);
        if (a == null) return NotFound();
        if (a.SubmittedAt == null) return RedirectToAction(nameof(Take), new { id });
        return View(a);
    }

    private async Task Grade(Attempt a, Dictionary<string, string> picked)
    {
        var questions = a.Exam!.Questions;
        a.Answers.Clear();
        var score = 0;
        foreach (var q in questions)
        {
            picked.TryGetValue(q.Id.ToString(), out var sel);
            if (sel != null && !q.Options().Any(o => o.Key == sel)) sel = null; // ignore tampered values
            if (sel == q.CorrectOption) score += q.Marks;
            a.Answers.Add(new Answer { QuestionId = q.Id, Selected = sel });
        }
        a.Score = score;
        a.TotalMarks = questions.Sum(q => q.Marks);
        a.SubmittedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    /// <summary>Attempts whose time ran out without a submit (browser closed) are graded as-is (unanswered).</summary>
    private async Task AutoSubmitExpired()
    {
        var open = await db.Attempts.Include(a => a.Exam).ThenInclude(e => e!.Questions)
            .Where(a => a.StudentId == Me && a.SubmittedAt == null).ToListAsync();
        foreach (var a in open.Where(a => DateTime.UtcNow > Deadline(a, a.Exam!)))
            await Grade(a, new Dictionary<string, string>());
    }
}
