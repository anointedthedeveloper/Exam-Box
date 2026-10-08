using System.Security.Claims;
using ExamBox.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;

namespace ExamBox.Services;

/// <summary>Institution name shown beside the logo (defaults to "ExamBox").</summary>
public sealed record PortalBrand(string Name)
{
    public static PortalBrand From(string? name) => new(string.IsNullOrWhiteSpace(name) ? "ExamBox" : name.Trim());
}

/// <summary>Identifies one run of the portal; sign-ins from an earlier run are rejected.</summary>
public sealed record PortalBoot(string Id)
{
    public static PortalBoot New() => new(Guid.NewGuid().ToString("N"));
}

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
            new("boot", http.RequestServices.GetRequiredService<PortalBoot>().Id),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = persistent });
    }
}
