using ExamBox.Models;
using ExamBox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamBox.Controllers;

[Authorize(Roles = "Student"), Route("portal")]
public class PortalController(AttemptService attempts) : Controller
{
    private int Me => User.GetUserId()!.Value;

    [HttpGet("")]
    public IActionResult Index() => View(attempts.Home(Me));

    [HttpGet("exams")]
    public IActionResult Exams() => View(attempts.Home(Me));

    [HttpGet("completed")]
    public IActionResult Completed() => View(attempts.Home(Me));

    /// <summary>The "before you begin" page. Starting is a POST from here.</summary>
    [HttpGet("instructions/{examId:int}")]
    public IActionResult Instructions(int examId)
    {
        var home = attempts.Home(Me);
        var exam = home.Available.FirstOrDefault(e => e.Id == examId);
        if (exam != null) return View(exam);
        var running = home.InProgress.FirstOrDefault(a => a.ExamId == examId);
        if (running != null) return RedirectToAction(nameof(Take), new { id = running.Id });
        TempData["Error"] = "This exam is not available to you right now.";
        return RedirectToAction(nameof(Exams));
    }

    [HttpPost("start/{examId:int}")]
    public IActionResult Start(int examId)
    {
        var r = attempts.Start(Me, examId);
        if (!r.Ok) { TempData["Error"] = r.Error; return RedirectToAction(nameof(Index)); }
        return RedirectToAction(nameof(Take), new { id = r.Value });
    }

    [HttpGet("exam/{id:int}")]
    public IActionResult Take(int id)
    {
        var t = attempts.GetTake(Me, id);
        if (t == null) return attempts.IsSubmitted(Me, id) ? RedirectToAction(nameof(Result), new { id }) : NotFound();
        if (t.SecondsLeft <= 0) return RedirectToAction(nameof(Result), new { id });
        return View(new TakeExamVm { Attempt = t.Attempt, Exam = t.Exam, Questions = t.Questions, Saved = t.Saved, SecondsLeft = t.SecondsLeft });
    }

    private Dictionary<int, string> ReadAnswers()
    {
        var d = new Dictionary<int, string>();
        foreach (var (key, val) in Request.Form)
            if (key.StartsWith("q_") && int.TryParse(key.AsSpan(2), out var qid) && val.Count > 0) d[qid] = val[0] ?? "";
        return d;
    }

    [HttpPost("exam/{id:int}")]
    public IActionResult Submit(int id)
    {
        var r = attempts.Submit(Me, id, ReadAnswers());
        if (!r.Ok) return NotFound();
        TempData["Success"] = r.Value ? "Time was up — your exam was submitted automatically." : "Exam submitted.";
        return RedirectToAction(nameof(Result), new { id });
    }

    /// <summary>Autosave, called by the exam page every few seconds.</summary>
    [HttpPost("exam/{id:int}/save")]
    public IActionResult Save(int id)
    {
        var r = attempts.SaveDraft(Me, id, ReadAnswers());
        return Json(new { ok = r.Ok, left = r.Value, error = r.Error });
    }

    [HttpGet("result/{id:int}")]
    public IActionResult Result(int id)
    {
        var a = attempts.GetResult(Me, id);
        if (a == null) return attempts.GetTake(Me, id) != null ? RedirectToAction(nameof(Take), new { id }) : NotFound();
        return View(a);
    }

    /// <summary>Question pictures; only students who have started the exam may fetch them.</summary>
    [HttpGet("media/{questionId:int}")]
    public IActionResult Media(int questionId)
    {
        var q = attempts.GetImageFor(Me, questionId);
        if (q == null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(q.ImageData!, q.ImageType ?? "image/png");
    }
}
