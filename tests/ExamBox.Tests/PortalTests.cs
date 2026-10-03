using System.Net;
using System.Text.RegularExpressions;
using ExamBox.Models;
using ExamBox.Services;
using Xunit;

namespace ExamBox.Tests;

public class PortalTests
{
    private static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); l.Start();
        var p = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop(); return p;
    }

    private sealed class Client
    {
        public readonly HttpClient Http;
        public Client(string baseUrl) =>
            Http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true }) { BaseAddress = new Uri(baseUrl) };
        public async Task<(string Url, string Html)> Get(string path)
        {
            var r = await Http.GetAsync(path); return (r.RequestMessage!.RequestUri!.PathAndQuery, await r.Content.ReadAsStringAsync());
        }
        public async Task<(string Url, string Html)> Post(string path, Dictionary<string, string> form, string? tokenPage = null)
        {
            var (_, page) = await Get(tokenPage ?? path);
            var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
            form["__RequestVerificationToken"] = token;
            var r = await Http.PostAsync(path, new FormUrlEncodedContent(form));
            return (r.RequestMessage!.RequestUri!.PathAndQuery, await r.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Student_portal_end_to_end()
    {
        using var t = new TempDb();
        t.Auth.CreateAdmin("admin", "Passw0rdAdmin");
        var port = FreePort();
        await using var host = await PortalHost.StartAsync(t.Factory, port);
        var c = new Client($"http://127.0.0.1:{port}");

        // embedded static assets are served (this is what makes the exe self-contained)
        Assert.Equal(HttpStatusCode.OK, (await c.Http.GetAsync("/assets/css/site.css")).StatusCode);
        foreach (var asset in new[] { "img/logo.png", "img/favicon.ico", "img/slide-exams.svg", "img/slide-devices.svg", "js/site.js" })
            Assert.Equal(HttpStatusCode.OK, (await c.Http.GetAsync("/assets/" + asset)).StatusCode);

        // data an admin would create in the desktop app
        var exam = t.Exams.Save(0, "Math 101", "desc", 5, 50).Value!;
        t.Exams.SaveQuestion(exam.Id, 0, "2+2?", "3", "4", "5", null, "B", 2);
        t.Exams.SaveQuestion(exam.Id, 0, "3*3?", "9", "6", null, null, "A", 2);
        t.Exams.SetPublished(exam.Id, true);
        var draft = t.Exams.Save(0, "Hidden draft", null, 5, 50).Value!;
        var stu = t.Students.Create(new StudentInput("Stu Dent", "S001", null, null)).Value!;

        // unauthenticated -> login; admins cannot use the portal
        Assert.Contains("/account/login", (await c.Get("/portal")).Url);
        var bad = await c.Post("/account/login", new() { ["Identifier"] = "admin", ["Password"] = "Passw0rdAdmin" }, "/account/login");
        Assert.Contains("Invalid ID or password", bad.Html);
        Assert.DoesNotContain("desktop", bad.Html);
        Assert.DoesNotContain("Administrators sign in", bad.Html);
        var wrong = await c.Post("/account/login", new() { ["Identifier"] = "S001", ["Password"] = "nope" }, "/account/login");
        Assert.Contains("Invalid ID or password", wrong.Html);

        // temp password forces a change
        var login = await c.Post("/account/login", new() { ["Identifier"] = "s001", ["Password"] = stu.TempPassword }, "/account/login");
        Assert.Contains("/account/changepassword", login.Url);
        Assert.Contains("/account/changepassword", (await c.Get("/portal")).Url);
        var mismatch = await c.Post("/account/changepassword", new() { ["CurrentPassword"] = stu.TempPassword, ["NewPassword"] = "abc", ["ConfirmPassword"] = "abd" });
        Assert.Contains("do not match", mismatch.Html);
        var changed = await c.Post("/account/changepassword", new() { ["CurrentPassword"] = stu.TempPassword, ["NewPassword"] = "StudentPass1", ["ConfirmPassword"] = "StudentPass1" });
        Assert.EndsWith("/portal", changed.Url);
        Assert.Contains("Math 101", changed.Html);
        Assert.DoesNotContain("Hidden draft", changed.Html);

        // take the exam
        var started = await c.Post($"/portal/start/{exam.Id}", new(), "/portal");
        Assert.Contains("id=\"clock\"", started.Html);
        var qids = Regex.Matches(started.Html, "name=\"q_(\\d+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.Equal(2, qids.Count);
        var result = await c.Post(started.Url, new() { [$"q_{qids[0]}"] = "B", [$"q_{qids[1]}"] = "B" });
        Assert.Contains("50%", result.Html);
        Assert.Contains("2 of 4", result.Html);

        // one attempt only
        var again = await c.Post($"/portal/start/{exam.Id}", new(), "/portal");
        Assert.Contains("/portal/result/", again.Url);

        // deactivated student's existing session is rejected
        t.Students.Update(stu.Student.Id, new StudentInput("Stu Dent", "S001", null, null, false));
        Assert.Contains("/account/login", (await c.Get("/portal")).Url);
    }
}
