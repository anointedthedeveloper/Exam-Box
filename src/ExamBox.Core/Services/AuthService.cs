using ExamBox.Data;
using ExamBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Services;

public sealed record AuthResult(User? User, string? Error);

/// <summary>Sign-in, lockout and account bootstrap, shared by the desktop app and the student portal.</summary>
public sealed class AuthService(DbFactory factory)
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockFor = TimeSpan.FromMinutes(10);

    public bool HasAdmin()
    {
        using var db = factory.Create();
        return db.Users.Any(u => u.Role == UserRole.Admin);
    }

    /// <summary>Creates the first administrator. Refused once any administrator exists.</summary>
    public OpResult<User> CreateAdmin(string fullName, string username, string? email, string password)
    {
        using var db = factory.Create();
        if (db.Users.Any(u => u.Role == UserRole.Admin)) return OpResult<User>.Fail("An administrator already exists.");
        fullName = fullName?.Trim() ?? ""; username = username?.Trim() ?? "";
        if (fullName.Length == 0) return OpResult<User>.Fail("Enter your full name.");
        if (username.Length == 0) return OpResult<User>.Fail("Enter a username.");
        var err = Passwords.Validate(password ?? "");
        if (err != null) return OpResult<User>.Fail(err);
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        var user = new User { FullName = fullName, Username = username, Email = email, Role = UserRole.Admin, PasswordHash = Passwords.Hash(password!) };
        db.Users.Add(user);
        db.SaveChanges();
        return OpResult<User>.Success(user);
    }

    public AuthResult Authenticate(string identifier, string password, UserRole role)
    {
        var id = (identifier ?? "").Trim();
        if (id.Length == 0 || string.IsNullOrEmpty(password)) return new(null, "Enter your ID and password.");
        using var db = factory.Create();
        var user = db.Users.FirstOrDefault(u => u.Username == id || u.Email == id);

        if (user != null && user.LockoutEnd > DateTime.UtcNow)
        {
            var mins = (int)Math.Ceiling((user.LockoutEnd!.Value - DateTime.UtcNow).TotalMinutes);
            return new(null, $"Too many failed attempts. Try again in {mins} minute(s).");
        }
        if (user == null || !Passwords.Verify(user.PasswordHash, password))
        {
            if (user != null)
            {
                user.FailedLogins++;
                if (user.FailedLogins >= MaxFailures) { user.LockoutEnd = DateTime.UtcNow + LockFor; user.FailedLogins = 0; }
                db.SaveChanges();
            }
            return new(null, "Invalid ID or password.");
        }
        if (user.Role != role)
            return new(null, role == UserRole.Admin ? "This is not an administrator account." : "Administrators sign in with the ExamBox desktop app.");
        if (!user.IsActive) return new(null, "This account has been deactivated. Contact your administrator.");

        user.FailedLogins = 0; user.LockoutEnd = null; user.LastLoginAt = DateTime.UtcNow;
        db.SaveChanges();
        return new(user, null);
    }

    public OpResult ChangePassword(int userId, string current, string next)
    {
        using var db = factory.Create();
        var user = db.Users.Find(userId);
        if (user == null) return OpResult.Fail("Account not found.");
        if (!Passwords.Verify(user.PasswordHash, current ?? "")) return OpResult.Fail("Current password is incorrect.");
        var err = Passwords.Validate(next ?? "");
        if (err != null) return OpResult.Fail(err);
        if (next == current) return OpResult.Fail("Choose a password different from the current one.");
        user.PasswordHash = Passwords.Hash(next!);
        user.MustChangePassword = false;
        db.SaveChanges();
        return OpResult.Success();
    }
}
