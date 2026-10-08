using ExamBox.Data;
using ExamBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Services;

public sealed record ClassRow(int Id, string Name, int Students, int Exams, DateTime CreatedUtc);

public sealed class ClassService(DbFactory factory)
{
    public List<string> Names()
    {
        using var db = factory.Create();
        return db.Classes.AsNoTracking().OrderBy(c => c.Name).Select(c => c.Name).ToList();
    }

    public List<ClassRow> List()
    {
        using var db = factory.Create();
        var students = db.Users.Where(u => u.Role == UserRole.Student && u.Department != null).GroupBy(u => u.Department!).Select(g => new { g.Key, N = g.Count() }).ToList();
        var exams = db.Exams.Where(e => e.ForDepartment != null).GroupBy(e => e.ForDepartment!).Select(g => new { g.Key, N = g.Count() }).ToList();
        return db.Classes.AsNoTracking().OrderBy(c => c.Name).AsEnumerable().Select(c => new ClassRow(c.Id, c.Name,
            students.FirstOrDefault(s => string.Equals(s.Key, c.Name, StringComparison.OrdinalIgnoreCase))?.N ?? 0,
            exams.FirstOrDefault(s => string.Equals(s.Key, c.Name, StringComparison.OrdinalIgnoreCase))?.N ?? 0, c.CreatedAt)).ToList();
    }

    private static string? Check(string? name)
    {
        name = name?.Trim() ?? "";
        if (name.Length == 0) return "Enter a class name.";
        if (name.Length > 80) return "The class name is too long.";
        return null;
    }

    public int Unassigned()
    {
        using var db = factory.Create();
        return db.Users.Count(u => u.Role == UserRole.Student && (u.Department == null || u.Department == ""));
    }

    public OpResult<SchoolClass> Create(string name)
    {
        var err = Check(name); if (err != null) return OpResult<SchoolClass>.Fail(err);
        name = name.Trim();
        using var db = factory.Create();
        if (db.Classes.Any(c => c.Name == name)) return OpResult<SchoolClass>.Fail("A class with this name already exists.");
        var c = new SchoolClass { Name = name };
        db.Classes.Add(c); db.SaveChanges();
        return OpResult<SchoolClass>.Success(c);
    }

    /// <summary>Makes sure a class exists (used by imports); returns its stored spelling.</summary>
    public string? Ensure(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        name = name.Trim();
        using var db = factory.Create();
        var c = db.Classes.FirstOrDefault(x => x.Name == name);
        if (c == null) { c = new SchoolClass { Name = name }; db.Classes.Add(c); db.SaveChanges(); }
        return c.Name;
    }

    /// <summary>Renames a class and keeps every student and exam that used the old name.</summary>
    public OpResult Rename(int id, string newName)
    {
        var err = Check(newName); if (err != null) return OpResult.Fail(err);
        newName = newName.Trim();
        using var db = factory.Create();
        var c = db.Classes.Find(id);
        if (c == null) return OpResult.Fail("Class not found.");
        if (db.Classes.Any(x => x.Id != id && x.Name == newName)) return OpResult.Fail("A class with this name already exists.");
        var old = c.Name;
        using var tx = db.Database.BeginTransaction();
        c.Name = newName;
        db.SaveChanges();
        db.Users.Where(u => u.Department == old).ExecuteUpdate(s => s.SetProperty(u => u.Department, newName));
        db.Exams.Where(e => e.ForDepartment == old).ExecuteUpdate(s => s.SetProperty(e => e.ForDepartment, newName));
        tx.Commit();
        return OpResult.Success();
    }

    /// <summary>A class that still has students cannot be deleted; exams that targeted it become open to everyone.</summary>
    public OpResult Delete(int id)
    {
        using var db = factory.Create();
        var c = db.Classes.Find(id);
        if (c == null) return OpResult.Fail("Class not found.");
        var n = db.Users.Count(u => u.Role == UserRole.Student && u.Department == c.Name);
        if (n > 0) return OpResult.Fail($"{n} student(s) are in this class. Move or delete them first.");
        db.Exams.Where(e => e.ForDepartment == c.Name).ExecuteUpdate(s => s.SetProperty(e => e.ForDepartment, (string?)null));
        db.Classes.Remove(c); db.SaveChanges();
        return OpResult.Success();
    }
}
