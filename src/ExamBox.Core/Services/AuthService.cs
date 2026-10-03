using ExamBox.Data;
using ExamBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Services;

/// <param name="AttemptsLeft">Set when a wrong password was entered for a real account and few tries remain.</param>
/// <param name="LockedUntil">Set (UTC) when the account is currently locked.</param>
public sealed record AuthResult(User? User, string? Error, int? AttemptsLeft = null, DateTime? LockedUntil = null);

/// <summary>Sign-in, lockout and account bootstrap, shared by the desktop app and the student portal.</summary>
public sealed class AuthService(DbFactory factory)
{
    public const int MaxFailures = 10;
    public const int LockMinutes = 5;
    /// <summary>Start warning "N attempts left" once this few remain.</summary>
    public const int WarnWhenLeft = 3;
    private static readonly TimeSpan LockFor = TimeSpan.FromMinutes(LockMinutes);

    public bool HasAdmin()
    {
        using var db = factory.Create();
        return db.Users.Any(u => u.Role == UserRole.Admin);
    }

    /// <summary>Creates the first administrator (the username is also the display name). Refused once any administrator exists.</summary>
    public OpResult<User> CreateAdmin(string username, string password)
    {
        using var db = factory.Create();
        if (db.Users.Any(u => u.Role == UserRole.Admin)) return OpResult<User>.Fail("An administrator already exists.");
        username = username?.Trim() ?? "";
        if (username.Length == 0) return OpResult<User>.Fail("Enter a username.");
        if (username.Length > 60) return OpResult<User>.Fail("Username is too long.");
        var err = Passwords.Validate(password ?? "");
        if (err != null) return OpResult<User>.Fail(err);
        var user = new User { FullName = username, Username = username, Role = UserRole.Admin, PasswordHash = Passwords.Hash(password!) };
        db.Users.Add(user);
        db.SaveChanges();
        return OpResult<User>.Success(user);
    }

    public AuthResult Authenticate(string identifier, string password, UserRole role)
    {
        var id = (identifier ?? "").Trim();
        var who = role == UserRole.Admin ? "username" : "ID";
        if (id.Length == 0 || string.IsNullOrEmpty(password)) return new(null, $"Enter your {who} and password.");
        // Identical wording for unknown user, wrong password and wrong account type, so one portal never reveals
        // that an account exists on the other.
        var generic = $"Invalid {who} or password.";
        using var db = factory.Create();
        var user = db.Users.FirstOrDefault(u => u.Username == id || u.Email == id);

        if (user != null && user.Role == role && user.LockoutEnd > DateTime.UtcNow)
        {
            var mins = (int)Math.Ceiling((user.LockoutEnd!.Value - DateTime.UtcNow).TotalMinutes);
            return new(null, $"Too many failed attempts. Try again in {mins} minute(s).", null, user.LockoutEnd);
        }
        if (user == null || user.Role != role) return new(null, generic);

        if (!Passwords.Verify(user.PasswordHash, password))
        {
            user.FailedLogins++;
            if (user.FailedLogins >= MaxFailures)
            {
                user.LockoutEnd = DateTime.UtcNow + LockFor;
                user.FailedLogins = 0;
                db.SaveChanges();
                return new(null, $"Too many failed attempts. This account is locked for {LockMinutes} minutes.", 0, user.LockoutEnd);
            }
            db.SaveChanges();
            var left = MaxFailures - user.FailedLogins;
            return left <= WarnWhenLeft
                ? new(null, $"{generic} {left} attempt{(left == 1 ? "" : "s")} left before a {LockMinutes}-minute lock.", left)
                : new(null, generic);
        }
        if (!user.IsActive) return new(null, "This account has been deactivated. Contact your administrator.");

        user.FailedLogins = 0; user.LockoutEnd = null; user.LastLoginAt = DateTime.UtcNow;
        db.SaveChanges();
        return new(user, null);
    }

    /// <summary>Recovery for a forgotten administrator password (run locally via <c>ExamBox.exe --reset-admin</c>).</summary>
    public OpResult<string> ResetAdminPassword(string newPassword)
    {
        var err = Passwords.Validate(newPassword ?? "");
        if (err != null) return OpResult<string>.Fail(err);
        using var db = factory.Create();
        var admin = db.Users.Where(u => u.Role == UserRole.Admin).OrderBy(u => u.Id).FirstOrDefault();
        if (admin == null) return OpResult<string>.Fail("No administrator account exists yet.");
        admin.PasswordHash = Passwords.Hash(newPassword!);
        admin.MustChangePassword = false; admin.FailedLogins = 0; admin.LockoutEnd = null;
        db.SaveChanges();
        return OpResult<string>.Success(admin.Username);
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
