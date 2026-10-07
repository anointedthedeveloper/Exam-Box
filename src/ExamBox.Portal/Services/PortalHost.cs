using System.Reflection;
using ExamBox.Data;
using ExamBox.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace ExamBox.Services;

/// <summary>Runs the student web portal inside any host process (desktop app or console).</summary>
public sealed class PortalHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    public int Port { get; }

    private PortalHost(WebApplication app, int port) { _app = app; Port = port; }

    /// <summary>Starts the portal listening on all network interfaces. Throws if the port is unavailable.</summary>
    public static async Task<PortalHost> StartAsync(DbFactory db, int port, string? brand = null)
    {
        var asm = typeof(PortalHost).Assembly;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = asm.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.ListenAnyIP(port));

        builder.Services.AddSingleton(db);
        builder.Services.AddSingleton(PortalBoot.New());
        builder.Services.AddSingleton(PortalBrand.From(brand));
        builder.Services.AddScoped<AuthService>();
        builder.Services.AddDbContext<AppDb>(o => o.UseSqlite(db.ConnectionString));
        builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()))
            .AddApplicationPart(asm);
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(db.DataDir, "keys")))
            .SetApplicationName("ExamBox");
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.LoginPath = "/account/login";
                o.AccessDeniedPath = "/account/denied";
                o.Cookie.Name = "exambox.auth";
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.ExpireTimeSpan = TimeSpan.FromHours(2);
                o.SlidingExpiration = true;
                // Reject cookies of users who were deactivated or deleted after signing in.
                o.Events.OnValidatePrincipal = async ctx =>
                {
                    var id = ctx.Principal?.GetUserId();
                    var ctxDb = ctx.HttpContext.RequestServices.GetRequiredService<AppDb>();
                    var boot = ctx.HttpContext.RequestServices.GetRequiredService<PortalBoot>().Id;
                    // Sign-ins do not survive closing/restarting ExamBox, whether or not the student signed out.
                    var ok = ctx.Principal?.FindFirst("boot")?.Value == boot && id != null && await ctxDb.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.IsActive);
                    if (!ok) { ctx.RejectPrincipal(); await ctx.HttpContext.SignOutAsync(); }
                };
            });
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseExceptionHandler("/home/error");
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new ManifestEmbeddedFileProvider(asm, "Assets"),
            RequestPath = "/assets",
        });
        app.UseRouting();
        app.UseAuthentication();
        app.UseMiddleware<ForcePasswordChangeMiddleware>();
        app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

        await app.StartAsync();
        return new PortalHost(app, port);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    /// <summary>Addresses students can type into their browser (this machine's LAN IPv4 addresses + localhost).</summary>
    public static IEnumerable<string> ReachableUrls(int port)
    {
        foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    yield return $"http://{ua.Address}:{port}";
        }
        yield return $"http://localhost:{port}";
    }
}
