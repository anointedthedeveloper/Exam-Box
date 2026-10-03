using ExamBox.Data;
using ExamBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Services;

public sealed record StudentInput(string FullName, string Username, string? Email, string? Department, bool IsActive = true);
public sealed record CreatedStudent(User Student, string TempPassword);

public sealed class StudentService(DbFactory factory)
{
    public List<User> List(string? search = null)
    {
        using var db = factory.Create();
        var q = db.Users.AsNoTracking().Include(u => u.Attempts).Where(u => u.Role == UserRole.Student);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u => EF.Functions.Like(u.FullName, $"%{s}%") || EF.Functions.Like(u.Username, $"%{s}%")
                             || (u.Email != null && EF.Functions.Like(u.Email, $"%{s}%")));
        }
        return q.OrderBy(u => u.FullName).ToList();
    }

    public User? Get(int id)
    {
        using var db = factory.Create();
        return db.Users.AsNoTracking().Include(u => u.Attempts).ThenInclude(a => a.Exam)
            .FirstOrDefault(u => u.Id == id && u.Role == UserRole.Student);
    }

    private static string? Check(StudentInput i)
    {
        if (string.IsNullOrWhiteSpace(i.FullName)) return "Enter the student's full name.";
        if (string.IsNullOrWhiteSpace(i.Username)) return "Enter a student ID.";
        if (i.FullName.Trim().Length > 120) return "Name is too long.";
        if (i.Username.Trim().Length > 60) return "Student ID is too long.";
        if (!string.IsNullOrWhiteSpace(i.Email) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(i.Email.Trim()))
            return "Enter a valid email address.";
        return null;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public OpResult<CreatedStudent> Create(StudentInput i)
    {
        var err = Check(i);
        if (err != null) return OpResult<CreatedStudent>.Fail(err);
        using var db = factory.Create();
        var username = i.Username.Trim(); var email = Clean(i.Email);
        if (db.Users.Any(u => u.Username == username)) return OpResult<CreatedStudent>.Fail("A user with this ID already exists.");
        if (email != null && db.Users.Any(u => u.Email == email)) return OpResult<CreatedStudent>.Fail("A user with this email already exists.");
        var temp = Passwords.Generate();
        var s = new User
        {
            FullName = i.FullName.Trim(), Username = username, Email = email, Department = Clean(i.Department),
            IsActive = i.IsActive, Role = UserRole.Student, PasswordHash = Passwords.Hash(temp), MustChangePassword = true,
        };
        db.Users.Add(s);
        db.SaveChanges();
        return OpResult<CreatedStudent>.Success(new CreatedStudent(s, temp));
    }

    public OpResult Update(int id, StudentInput i)
    {
        var err = Check(i);
        if (err != null) return OpResult.Fail(err);
        using var db = factory.Create();
        var s = db.Users.FirstOrDefault(u => u.Id == id && u.Role == UserRole.Student);
        if (s == null) return OpResult.Fail("Student not found.");
        var username = i.Username.Trim(); var email = Clean(i.Email);
        if (db.Users.Any(u => u.Id != id && u.Username == username)) return OpResult.Fail("A user with this ID already exists.");
        if (email != null && db.Users.Any(u => u.Id != id && u.Email == email)) return OpResult.Fail("A user with this email already exists.");
        s.FullName = i.FullName.Trim(); s.Username = username; s.Email = email; s.Department = Clean(i.Department); s.IsActive = i.IsActive;
        db.SaveChanges();
        return OpResult.Success();
    }

    public OpResult<string> ResetPassword(int id)
    {
        using var db = factory.Create();
        var s = db.Users.FirstOrDefault(u => u.Id == id && u.Role == UserRole.Student);
        if (s == null) return OpResult<string>.Fail("Student not found.");
        var temp = Passwords.Generate();
        s.PasswordHash = Passwords.Hash(temp); s.MustChangePassword = true; s.FailedLogins = 0; s.LockoutEnd = null;
        db.SaveChanges();
        return OpResult<string>.Success(temp);
    }

    public OpResult Delete(int id)
    {
        using var db = factory.Create();
        var s = db.Users.FirstOrDefault(u => u.Id == id && u.Role == UserRole.Student);
        if (s == null) return OpResult.Fail("Student not found.");
        db.Users.Remove(s);
        db.SaveChanges();
        return OpResult.Success();
    }
}
