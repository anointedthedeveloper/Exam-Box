using System.Security.Claims;
using ExamBox.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ExamBox.Services;

public static class AuthExtensions
{
    public static int? GetUserId(this ClaimsPrincipal p) =>
        int.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static Task SignInUserAsync(this HttpContext http, User u, bool persistent = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.FullName),
            new("username", u.Username),
            new(ClaimTypes.Role, u.Role.ToString()),
        };
        if (u.MustChangePassword) claims.Add(new Claim("mcp", "1"));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = persistent });
    }
}

/// <summary>Users flagged with a temporary password can only change it (or log out) until they do.</summary>
public class ForcePasswordChangeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        if (ctx.User.Identity?.IsAuthenticated == true && ctx.User.HasClaim("mcp", "1"))
        {
            var path = ctx.Request.Path.Value ?? "";
            var allowed = path.StartsWith("/account/changepassword", StringComparison.OrdinalIgnoreCase)
                          || path.StartsWith("/account/logout", StringComparison.OrdinalIgnoreCase)
                          || path.StartsWith("/assets");
            if (!allowed)
            {
                ctx.Response.Redirect("/account/changepassword");
                return;
            }
        }
        await next(ctx);
    }
}
