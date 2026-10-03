using ExamBox.Data;
using ExamBox.Models;
using ExamBox.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction("Login", "Account");
        return User.IsInRole("Admin") ? RedirectToAction("Index", "Dashboard") : RedirectToAction("Index", "Portal");
    }

    [Route("home/error")]
    public IActionResult Error() => View();
}

[Route("account")]
public class AccountController(AppDb db) : Controller
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockFor = TimeSpan.FromMinutes(10);

    [HttpGet("login")]
    public async Task<IActionResult> Login(string? portal, string? returnUrl)
    {
        if (!await db.Users.AnyAsync(u => u.Role == UserRole.Admin)) return RedirectToAction("Index", "Setup");
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new LoginVm { Portal = portal == "staff" ? "staff" : "student", ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var id = vm.Identifier.Trim();
        var wantAdmin = vm.Portal == "staff";
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == id || u.Email == id);

        if (user != null && user.LockoutEnd > DateTime.UtcNow)
        {
            var mins = (int)Math.Ceiling((user.LockoutEnd!.Value - DateTime.UtcNow).TotalMinutes);
            ModelState.AddModelError("", $"Too many failed attempts. Try again in {mins} minute(s).");
            return View(vm);
        }

        // Same generic error for unknown user / wrong password / wrong portal.
        if (user == null || !Passwords.Verify(user.PasswordHash, vm.Password))
        {
            if (user != null)
            {
                user.FailedLogins++;
                if (user.FailedLogins >= MaxFailures) { user.LockoutEnd = DateTime.UtcNow + LockFor; user.FailedLogins = 0; }
                await db.SaveChangesAsync();
            }
            ModelState.AddModelError("", "Invalid ID or password.");
            return View(vm);
        }
        if ((user.Role == UserRole.Admin) != wantAdmin)
        {
            ModelState.AddModelError("", wantAdmin ? "This account is not a staff account. Use the Student tab." : "This is a staff account. Use the Staff tab.");
            return View(vm);
        }
        if (!user.IsActive)
        {
            ModelState.AddModelError("", "This account has been deactivated. Contact your administrator.");
            return View(vm);
        }

        user.FailedLogins = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await HttpContext.SignInUserAsync(user, vm.RememberMe);

        if (user.MustChangePassword) return RedirectToAction(nameof(ChangePassword));
        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl)) return LocalRedirect(vm.ReturnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [Authorize, HttpGet("changepassword")]
    public IActionResult ChangePassword() => View(new ChangePasswordVm());

    [Authorize, HttpPost("changepassword")]
    public async Task<IActionResult> ChangePassword(ChangePasswordVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var user = await db.Users.FindAsync(User.GetUserId());
        if (user == null) return Forbid();
        if (!Passwords.Verify(user.PasswordHash, vm.CurrentPassword))
        {
            ModelState.AddModelError(nameof(vm.CurrentPassword), "Current password is incorrect.");
            return View(vm);
        }
        var err = Passwords.Validate(vm.NewPassword);
        if (err != null) { ModelState.AddModelError(nameof(vm.NewPassword), err); return View(vm); }
        if (vm.NewPassword == vm.CurrentPassword)
        {
            ModelState.AddModelError(nameof(vm.NewPassword), "Choose a password different from the current one.");
            return View(vm);
        }
        user.PasswordHash = Passwords.Hash(vm.NewPassword);
        user.MustChangePassword = false;
        await db.SaveChangesAsync();
        await HttpContext.SignInUserAsync(user); // refresh claims (drops mcp)
        TempData["Success"] = "Password updated.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("denied")]
    public IActionResult Denied() => View();
}

/// <summary>First-run wizard: creates the initial administrator. Disabled once an admin exists.</summary>
[Route("setup")]
public class SetupController(AppDb db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin)) return RedirectToAction("Login", "Account", new { portal = "staff" });
        return View(new SetupVm());
    }

    [HttpPost("")]
    public async Task<IActionResult> Index(SetupVm vm)
    {
        if (await db.Users.AnyAsync(u => u.Role == UserRole.Admin)) return RedirectToAction("Login", "Account", new { portal = "staff" });
        var err = Passwords.Validate(vm.Password ?? "");
        if (err != null) ModelState.AddModelError(nameof(vm.Password), err);
        if (!ModelState.IsValid) return View(vm);
        var username = vm.Username.Trim();
        if (await db.Users.AnyAsync(u => u.Username == username))
        {
            ModelState.AddModelError(nameof(vm.Username), "That username is taken.");
            return View(vm);
        }
        db.Users.Add(new User
        {
            FullName = vm.FullName.Trim(),
            Username = username,
            Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim(),
            PasswordHash = Passwords.Hash(vm.Password!),
            Role = UserRole.Admin,
        });
        await db.SaveChangesAsync();
        TempData["Success"] = "Administrator created. Sign in to continue.";
        return RedirectToAction("Login", "Account", new { portal = "staff" });
    }
}
