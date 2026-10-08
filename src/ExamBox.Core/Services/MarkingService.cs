using ExamBox.Data;
using ExamBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Services;

public sealed record PendingRow(int AttemptId, string Student, string StudentId, string Exam, int ExamId, DateTime SubmittedUtc, int TheoryMax, int ObjectiveScore);

public sealed class MarkingSheet
{
    public Attempt Attempt { get; init; } = null!;
    public Exam Exam { get; init; } = null!;
    public User Student { get; init; } = null!;
    /// <summary>Theory questions only, in exam order, with the student's answer.</summary>
    public List<(Question Question, Answer Answer)> Items { get; init; } = new();
}

public sealed record MarkEntry(int QuestionId, int? Marks, string? Comment);

public sealed class MarkingService(DbFactory factory)
{
    public int PendingCount()
    {
        using var db = factory.Create();
        return db.Attempts.Count(a => a.SubmittedAt != null && a.PendingMarking);
    }

    public List<PendingRow> Pending(int? examId = null)
    {
        using var db = factory.Create();
        var q = db.Attempts.AsNoTracking().Where(a => a.SubmittedAt != null && a.PendingMarking);
        if (examId != null) q = q.Where(a => a.ExamId == examId);
        return q.OrderBy(a => a.SubmittedAt).Select(a => new
        {
            a.Id, Name = a.Student!.FullName, Sid = a.Student.Username, Exam = a.Exam!.Title, a.ExamId, At = a.SubmittedAt!.Value,
            Max = a.Exam.Questions.Where(x => x.Type == QuestionType.Theory).Sum(x => x.Marks), a.ObjectiveScore,
        }).AsEnumerable().Select(x => new PendingRow(x.Id, x.Name, x.Sid, x.Exam, x.ExamId, x.At, x.Max, x.ObjectiveScore)).ToList();
    }

    public MarkingSheet? Sheet(int attemptId)
    {
        using var db = factory.Create();
        var a = db.Attempts.AsNoTracking().Include(x => x.Student).Include(x => x.Answers)
            .Include(x => x.Exam).ThenInclude(e => e!.Questions).FirstOrDefault(x => x.Id == attemptId);
        if (a?.SubmittedAt == null) return null;
        var items = a.Exam!.Questions.Where(q => q.Type == QuestionType.Theory && q.Marks > 0).OrderBy(q => q.SortOrder).ThenBy(q => q.Id)
            .Select(q => (q, a.Answers.FirstOrDefault(x => x.QuestionId == q.Id) ?? new Answer { QuestionId = q.Id, AttemptId = a.Id }))
            .ToList();
        var withPics = items.Where(i => i.Item1.HasImage).Select(i => i.Item1).ToList();
        if (withPics.Count > 0)
        {
            var ids = withPics.Select(q => q.Id).ToList();
            var pics = db.Images.AsNoTracking().Where(i => ids.Contains(i.QuestionId)).ToDictionary(i => i.QuestionId, i => i.Data);
            foreach (var q in withPics) q.ImageData = pics.GetValueOrDefault(q.Id);
        }
        return new MarkingSheet { Attempt = a, Exam = a.Exam, Student = a.Student!, Items = items };
    }

    /// <summary>
    /// Stores the teacher's marks. With <paramref name="finish"/> every scored theory question must have marks;
    /// the attempt is then finalized and appears in results and reports. Without it, progress is just saved.
    /// </summary>
    public OpResult Save(int attemptId, IReadOnlyList<MarkEntry> entries, bool finish)
    {
        using var db = factory.Create();
        var a = db.Attempts.Include(x => x.Answers).Include(x => x.Exam).ThenInclude(e => e!.Questions)
            .FirstOrDefault(x => x.Id == attemptId);
        if (a?.SubmittedAt == null) return OpResult.Fail("This attempt has not been submitted.");
        var theory = a.Exam!.Questions.Where(q => q.Type == QuestionType.Theory).ToDictionary(q => q.Id);
        foreach (var e in entries)
        {
            if (!theory.TryGetValue(e.QuestionId, out var q)) return OpResult.Fail("Unknown question.");
            if (e.Marks is < 0 || e.Marks > q.Marks) return OpResult.Fail($"Marks for question {Label(q)} must be between 0 and {q.Marks}.");
            if (e.Comment is { Length: > 1000 }) return OpResult.Fail("A comment is too long (1000 characters at most).");
        }
        foreach (var e in entries)
        {
            var row = a.Answers.FirstOrDefault(x => x.QuestionId == e.QuestionId);
            if (row == null) { row = new Answer { QuestionId = e.QuestionId }; a.Answers.Add(row); }
            row.Marks = e.Marks;
            row.Comment = string.IsNullOrWhiteSpace(e.Comment) ? null : e.Comment.Trim();
        }
        var unmarked = theory.Values.Where(q => q.Marks > 0)
            .Where(q => a.Answers.FirstOrDefault(x => x.QuestionId == q.Id)?.Marks == null).ToList();
        if (finish && unmarked.Count > 0)
            return OpResult.Fail($"Give a mark (even 0) for question {Label(unmarked[0])}" + (unmarked.Count > 1 ? $" and {unmarked.Count - 1} more." : "."));

        a.TheoryScore = theory.Keys.Sum(id => a.Answers.FirstOrDefault(x => x.QuestionId == id)?.Marks ?? 0);
        a.Score = a.ObjectiveScore + a.TheoryScore;
        if (finish) { a.PendingMarking = false; a.MarkedAt = DateTime.UtcNow; }
        else if (unmarked.Count == 0 && !a.PendingMarking) { a.MarkedAt = DateTime.UtcNow; } // re-edit of a finished attempt
        db.SaveChanges();
        return OpResult.Success();
    }

    public static string Label(Question q) => string.IsNullOrWhiteSpace(q.Number) ? $"#{q.SortOrder}" : q.Number!;
}
