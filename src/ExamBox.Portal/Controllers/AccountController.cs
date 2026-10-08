using Microsoft.EntityFrameworkCore;
using ExamBox.Models;
using ExamBox.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExamBox.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() =>
        User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Portal") : RedirectToAction("Login", "Account");

    [Route("home/error")]
    public IActionResult Error() => View();
}

[Route("account")]
public class AccountController(AuthService auth) : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl, string? ended = null)
    {
        ViewBag.Ended = !string.IsNullOrEmpty(ended);
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        ViewBag.Ended = false;
        if (!ModelState.IsValid) return View(vm);
        var r = auth.Authenticate(vm.Identifier, vm.Password, UserRole.Student);
        if (r.User == null)
        {
            ModelState.AddModelError("", r.Error ?? "Sign-in failed.");
            return View(vm);
        }
        await HttpContext.SignInUserAsync(r.User);
        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl)) return LocalRedirect(vm.ReturnUrl);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    /// <summary>The student's own profile. Passwords are managed by the teacher/admin, not here.</summary>
    [Authorize, HttpGet("me")]
    public IActionResult Me([FromServices] ExamBox.Data.DbFactory factory, [FromServices] PasswordVault vault)
    {
        var id = User.GetUserId();
        if (id == null) return Forbid();
        using var db = factory.Create();
        var u = db.Users.AsNoTracking().Include(x => x.Attempts).ThenInclude(a => a.Exam).FirstOrDefault(x => x.Id == id.Value);
        if (u == null) return Forbid();
        ViewBag.Password = vault.Reveal(u.PasswordCipher);
        return View(u);
    }

    [Authorize, HttpGet("changepassword")]
    public IActionResult ChangePassword() => RedirectToActionPermanent(nameof(Me));

    [HttpGet("denied")]
    public IActionResult Denied() => View();
}
