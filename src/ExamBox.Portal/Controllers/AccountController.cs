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
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var r = auth.Authenticate(vm.Identifier, vm.Password, UserRole.Student);
        if (r.User == null)
        {
            ModelState.AddModelError("", r.Error ?? "Sign-in failed.");
            return View(vm);
        }
        await HttpContext.SignInUserAsync(r.User, vm.RememberMe);
        if (r.User.MustChangePassword) return RedirectToAction(nameof(ChangePassword));
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
    public async Task<IActionResult> ChangePassword(ChangePasswordVm vm, [FromServices] ExamBox.Data.DbFactory factory)
    {
        if (!ModelState.IsValid) return View(vm);
        var id = User.GetUserId();
        if (id == null) return Forbid();
        var r = auth.ChangePassword(id.Value, vm.CurrentPassword, vm.NewPassword);
        if (!r.Ok)
        {
            ModelState.AddModelError(r.Error!.Contains("Current") ? nameof(vm.CurrentPassword) : nameof(vm.NewPassword), r.Error);
            return View(vm);
        }
        using (var db = factory.Create())
            await HttpContext.SignInUserAsync(db.Users.Find(id.Value)!); // refresh claims (drops the must-change flag)
        TempData["Success"] = "Password updated.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("denied")]
    public IActionResult Denied() => View();
}
