using ExamBox.Data;
using ExamBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Services;

public sealed class ExamService(DbFactory factory)
{
    public List<Exam> List()
    {
        using var db = factory.Create();
        return db.Exams.AsNoTracking().Include(e => e.Questions).Include(e => e.Attempts)
            .OrderByDescending(e => e.CreatedAt).ToList();
    }

    public Exam? Get(int id)
    {
        using var db = factory.Create();
        var e = db.Exams.AsNoTracking().Include(x => x.Questions).Include(x => x.Attempts).ThenInclude(a => a.Student)
            .FirstOrDefault(x => x.Id == id);
        e?.Questions.Sort((a, b) => a.Id.CompareTo(b.Id));
        return e;
    }

    public bool IsLocked(int examId)
    {
        using var db = factory.Create();
        return db.Attempts.Any(a => a.ExamId == examId);
    }

    /// <summary>Creates (Id == 0) or updates an exam's details. Publish state is changed only via <see cref="SetPublished"/>.</summary>
    public OpResult<Exam> Save(int id, string title, string? description, int durationMinutes, int passMarkPercent)
    {
        title = title?.Trim() ?? "";
        if (title.Length == 0) return OpResult<Exam>.Fail("Enter a title.");
        if (title.Length > 160) return OpResult<Exam>.Fail("Title is too long.");
        if (durationMinutes is < 1 or > 600) return OpResult<Exam>.Fail("Duration must be between 1 and 600 minutes.");
        if (passMarkPercent is < 1 or > 100) return OpResult<Exam>.Fail("Pass mark must be between 1 and 100.");
        using var db = factory.Create();
        Exam e;
        if (id == 0) { e = new Exam(); db.Exams.Add(e); }
        else
        {
            e = db.Exams.Find(id)!;
            if (e == null) return OpResult<Exam>.Fail("Exam not found.");
        }
        e.Title = title; e.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        e.DurationMinutes = durationMinutes; e.PassMarkPercent = passMarkPercent;
        db.SaveChanges();
        return OpResult<Exam>.Success(e);
    }

    public OpResult SetPublished(int id, bool published)
    {
        using var db = factory.Create();
        var e = db.Exams.Include(x => x.Questions).FirstOrDefault(x => x.Id == id);
        if (e == null) return OpResult.Fail("Exam not found.");
        if (published && e.Questions.Count == 0) return OpResult.Fail("Add at least one question before publishing.");
        e.IsPublished = published;
        db.SaveChanges();
        return OpResult.Success();
    }

    public OpResult Delete(int id)
    {
        using var db = factory.Create();
        var e = db.Exams.Find(id);
        if (e == null) return OpResult.Fail("Exam not found.");
        db.Exams.Remove(e);
        db.SaveChanges();
        return OpResult.Success();
    }

    // ---- questions: locked once any student has started, so grading stays consistent ----

    public OpResult SaveQuestion(int examId, int questionId, string text, string a, string b, string? c, string? d, string correct, int marks)
    {
        text = text?.Trim() ?? ""; a = a?.Trim() ?? ""; b = b?.Trim() ?? "";
        c = string.IsNullOrWhiteSpace(c) ? null : c.Trim();
        d = string.IsNullOrWhiteSpace(d) ? null : d.Trim();
        correct = (correct ?? "").Trim().ToUpperInvariant();
        if (text.Length == 0) return OpResult.Fail("Enter the question text.");
        if (a.Length == 0 || b.Length == 0) return OpResult.Fail("Options A and B are required.");
        if (d != null && c == null) return OpResult.Fail("Fill option C before option D.");
        if (correct is not ("A" or "B" or "C" or "D")) return OpResult.Fail("Choose the correct answer.");
        if ((correct == "C" && c == null) || (correct == "D" && d == null)) return OpResult.Fail("The correct answer must be one of the filled-in options.");
        if (marks is < 1 or > 100) return OpResult.Fail("Marks must be between 1 and 100.");
        if (text.Length > 2000) return OpResult.Fail("Question text is too long.");

        using var db = factory.Create();
        if (!db.Exams.Any(e => e.Id == examId)) return OpResult.Fail("Exam not found.");
        if (db.Attempts.Any(x => x.ExamId == examId)) return OpResult.Fail("Students have already started this exam; its questions are locked.");
        Question q;
        if (questionId == 0) { q = new Question { ExamId = examId }; db.Questions.Add(q); }
        else
        {
            q = db.Questions.FirstOrDefault(x => x.Id == questionId && x.ExamId == examId)!;
            if (q == null) return OpResult.Fail("Question not found.");
        }
        q.Text = text; q.OptionA = a; q.OptionB = b; q.OptionC = c; q.OptionD = d; q.CorrectOption = correct; q.Marks = marks;
        db.SaveChanges();
        return OpResult.Success();
    }

    public OpResult DeleteQuestion(int examId, int questionId)
    {
        using var db = factory.Create();
        if (db.Attempts.Any(x => x.ExamId == examId)) return OpResult.Fail("Students have already started this exam; its questions are locked.");
        var q = db.Questions.FirstOrDefault(x => x.Id == questionId && x.ExamId == examId);
        if (q == null) return OpResult.Fail("Question not found.");
        db.Questions.Remove(q);
        db.SaveChanges();
        // An exam with no questions can't stay published.
        if (!db.Questions.Any(x => x.ExamId == examId))
            db.Exams.Where(x => x.Id == examId).ExecuteUpdate(s => s.SetProperty(x => x.IsPublished, false));
        return OpResult.Success();
    }
}
