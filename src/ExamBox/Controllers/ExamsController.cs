using ExamBox.Data;
using ExamBox.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Controllers;

[Authorize(Roles = "Admin"), Route("admin/exams")]
public class ExamsController(AppDb db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index() =>
        View(await db.Exams.Include(e => e.Questions).Include(e => e.Attempts).OrderByDescending(e => e.CreatedAt).ToListAsync());

    [HttpGet("create")]
    public IActionResult Create() => View("Form", new Exam());

    [HttpPost("create")]
    public async Task<IActionResult> Create([Bind("Title,Description,DurationMinutes,PassMarkPercent")] Exam exam)
    {
        if (!ModelState.IsValid) return View("Form", exam);
        db.Exams.Add(exam);
        await db.SaveChangesAsync();
        TempData["Success"] = "Exam created. Add questions, then publish it.";
        return RedirectToAction(nameof(Details), new { id = exam.Id });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var e = await db.Exams.Include(x => x.Questions.OrderBy(q => q.Id)).Include(x => x.Attempts).FirstOrDefaultAsync(x => x.Id == id);
        return e == null ? NotFound() : View(e);
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var e = await db.Exams.FindAsync(id);
        return e == null ? NotFound() : View("Form", e);
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, [Bind("Title,Description,DurationMinutes,PassMarkPercent")] Exam input)
    {
        var e = await db.Exams.FindAsync(id);
        if (e == null) return NotFound();
        if (!ModelState.IsValid) { input.Id = id; return View("Form", input); }
        e.Title = input.Title; e.Description = input.Description;
        e.DurationMinutes = input.DurationMinutes; e.PassMarkPercent = input.PassMarkPercent;
        await db.SaveChangesAsync();
        TempData["Success"] = "Exam updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/toggle-publish")]
    public async Task<IActionResult> TogglePublish(int id)
    {
        var e = await db.Exams.Include(x => x.Questions).FirstOrDefaultAsync(x => x.Id == id);
        if (e == null) return NotFound();
        if (!e.IsPublished && e.Questions.Count == 0)
        {
            TempData["Error"] = "Add at least one question before publishing.";
            return RedirectToAction(nameof(Details), new { id });
        }
        e.IsPublished = !e.IsPublished;
        await db.SaveChangesAsync();
        TempData["Success"] = e.IsPublished ? "Exam published. Students can now see it." : "Exam unpublished.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var e = await db.Exams.FindAsync(id);
        if (e == null) return NotFound();
        db.Exams.Remove(e);
        await db.SaveChangesAsync();
        TempData["Success"] = "Exam deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ---- questions (locked once any student has started the exam, so grading stays consistent) ----

    private async Task<bool> IsLocked(int examId) => await db.Attempts.AnyAsync(a => a.ExamId == examId);

    [HttpGet("{id:int}/questions/add")]
    public async Task<IActionResult> AddQuestion(int id)
    {
        var e = await db.Exams.FindAsync(id);
        if (e == null) return NotFound();
        if (await IsLocked(id)) { TempData["Error"] = "Students have already started this exam; questions are locked."; return RedirectToAction(nameof(Details), new { id }); }
        ViewBag.Exam = e;
        return View("QuestionForm", new Question { ExamId = id });
    }

    [HttpPost("{id:int}/questions/add")]
    public async Task<IActionResult> AddQuestion(int id, [Bind("Text,OptionA,OptionB,OptionC,OptionD,CorrectOption,Marks")] Question q)
    {
        var e = await db.Exams.FindAsync(id);
        if (e == null) return NotFound();
        if (await IsLocked(id)) { TempData["Error"] = "Questions are locked."; return RedirectToAction(nameof(Details), new { id }); }
        q.ExamId = id;
        Normalize(q);
        if (!ModelState.IsValid) { ViewBag.Exam = e; return View("QuestionForm", q); }
        db.Questions.Add(q);
        await db.SaveChangesAsync();
        TempData["Success"] = "Question added.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet("{id:int}/questions/{qid:int}/edit")]
    public async Task<IActionResult> EditQuestion(int id, int qid)
    {
        var q = await db.Questions.Include(x => x.Exam).FirstOrDefaultAsync(x => x.Id == qid && x.ExamId == id);
        if (q == null) return NotFound();
        if (await IsLocked(id)) { TempData["Error"] = "Questions are locked."; return RedirectToAction(nameof(Details), new { id }); }
        ViewBag.Exam = q.Exam;
        return View("QuestionForm", q);
    }

    [HttpPost("{id:int}/questions/{qid:int}/edit")]
    public async Task<IActionResult> EditQuestion(int id, int qid, [Bind("Text,OptionA,OptionB,OptionC,OptionD,CorrectOption,Marks")] Question input)
    {
        var q = await db.Questions.Include(x => x.Exam).FirstOrDefaultAsync(x => x.Id == qid && x.ExamId == id);
        if (q == null) return NotFound();
        if (await IsLocked(id)) { TempData["Error"] = "Questions are locked."; return RedirectToAction(nameof(Details), new { id }); }
        Normalize(input);
        if (!ModelState.IsValid) { input.Id = qid; input.ExamId = id; ViewBag.Exam = q.Exam; return View("QuestionForm", input); }
        q.Text = input.Text; q.OptionA = input.OptionA; q.OptionB = input.OptionB;
        q.OptionC = input.OptionC; q.OptionD = input.OptionD; q.CorrectOption = input.CorrectOption; q.Marks = input.Marks;
        await db.SaveChangesAsync();
        TempData["Success"] = "Question updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/questions/{qid:int}/delete")]
    public async Task<IActionResult> DeleteQuestion(int id, int qid)
    {
        var q = await db.Questions.FirstOrDefaultAsync(x => x.Id == qid && x.ExamId == id);
        if (q == null) return NotFound();
        if (await IsLocked(id)) { TempData["Error"] = "Questions are locked."; return RedirectToAction(nameof(Details), new { id }); }
        db.Questions.Remove(q);
        await db.SaveChangesAsync();
        // An exam with no questions can't stay published.
        if (!await db.Questions.AnyAsync(x => x.ExamId == id))
            await db.Exams.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsPublished, false));
        TempData["Success"] = "Question deleted.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private void Normalize(Question q)
    {
        q.OptionC = string.IsNullOrWhiteSpace(q.OptionC) ? null : q.OptionC.Trim();
        q.OptionD = string.IsNullOrWhiteSpace(q.OptionD) ? null : q.OptionD.Trim();
        if ((q.CorrectOption == "C" && q.OptionC == null) || (q.CorrectOption == "D" && q.OptionD == null))
            ModelState.AddModelError(nameof(q.CorrectOption), "The correct answer must be one of the filled-in options.");
        if (q.OptionD != null && q.OptionC == null)
            ModelState.AddModelError(nameof(q.OptionC), "Fill option C before option D.");
    }

    // ---- results ----

    [HttpGet("{id:int}/results")]
    public async Task<IActionResult> Results(int id)
    {
        var e = await db.Exams.Include(x => x.Attempts).ThenInclude(a => a.Student).FirstOrDefaultAsync(x => x.Id == id);
        return e == null ? NotFound() : View(e);
    }
}
