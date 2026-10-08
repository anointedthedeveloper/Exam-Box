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

        // signs in with the password the admin set; no forced change, "My account" shows the profile
        var login = await c.Post("/account/login", new() { ["Identifier"] = "s001", ["Password"] = stu.Password }, "/account/login");
        Assert.EndsWith("/portal", login.Url);
        var me = await c.Get("/account/me");
        Assert.Contains("Stu Dent", me.Html); Assert.Contains("S001", me.Html); Assert.Contains("Managed by your teacher", me.Html);
        Assert.EndsWith("/account/me", (await c.Get("/account/changepassword")).Url);
        var changed = await c.Get("/portal");
        Assert.Contains("Math 101", changed.Html);
        Assert.DoesNotContain("Hidden draft", changed.Html);

        // take the exam
        var started = await c.Post($"/portal/start/{exam.Id}", new(), "/portal");
        Assert.Contains("id=\"clock\"", started.Html);
        var qids = Regex.Matches(started.Html, "name=\"q_(\\d+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.Equal(2, qids.Count);
        var result = await c.Post(started.Url, new() { [$"q_{qids[0]}"] = "B", [$"q_{qids[1]}"] = "B" });
        Assert.Contains("Exam completed", result.Html);
        Assert.DoesNotContain("50%", result.Html);            // students never see scores
        Assert.DoesNotContain("2 of 4", result.Html);

        // one attempt only
        var again = await c.Post($"/portal/start/{exam.Id}", new(), "/portal");
        Assert.Contains("/portal/result/", again.Url);

        // deactivated student's existing session is rejected
        t.Students.Update(stu.Student.Id, new StudentInput("Stu Dent", "S001", null, null, false));
        Assert.Contains("/account/login", (await c.Get("/portal")).Url);
    }

    [Fact]
    public async Task Sessions_end_when_the_portal_restarts()
    {
        using var t = new TempDb();
        t.Auth.CreateAdmin("admin", "pw");
        var stu = t.Students.Create(new StudentInput("Stu", "S9", null, null)).Value!;
        t.Auth.ChangePassword(stu.Student.Id, stu.Password, "pw2");
        var port = FreePort();
        var c = new Client($"http://127.0.0.1:{port}");
        await using (var host = await PortalHost.StartAsync(t.Factory, port))
        {
            var login = await c.Post("/account/login", new() { ["Identifier"] = "S9", ["Password"] = "pw2" }, "/account/login");
            Assert.EndsWith("/portal", login.Url);
            Assert.EndsWith("/portal", (await c.Get("/portal")).Url);
        }
        await using (var host2 = await PortalHost.StartAsync(t.Factory, port))
            Assert.Contains("/account/login", (await c.Get("/portal")).Url);   // same cookie, new run: must sign in again
    }

    [Fact]
    public async Task Institution_name_is_shown_on_the_student_site()
    {
        using var t = new TempDb();
        var port = FreePort();
        await using (var host = await PortalHost.StartAsync(t.Factory, port, "Greenfield Academy"))
            Assert.Contains("Greenfield Academy", (await new Client($"http://127.0.0.1:{port}").Get("/account/login")).Html);
        var port2 = FreePort();
        await using (var host = await PortalHost.StartAsync(t.Factory, port2))
            Assert.Contains("ExamBox", (await new Client($"http://127.0.0.1:{port2}").Get("/account/login")).Html);   // default brand
    }

    [Fact]
    public async Task Theory_exam_over_http_with_pictures_autosave_and_pending_marking()
    {
        using var t = new TempDb();
        t.Auth.CreateAdmin("admin", "pw");
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var exam = t.Exams.Save(0, "English", null, 20, 50).Value!;
        t.Exams.SaveQuestion(exam.Id, 0, "Pick one", "a", "b", null, null, "A", 1);
        t.Exams.Theory(exam.Id, "Describe the picture", 10, "2a");
        var withPic = t.Exams.Get(exam.Id)!.Questions.Last();
        t.Exams.SetImage(exam.Id, withPic.Id, png, "image/png");
        t.Exams.Launch(exam.Id, null, null, null);
        var s1 = t.Students.Create(new StudentInput("One", "P1", null, null)).Value!;
        var s2 = t.Students.Create(new StudentInput("Two", "P2", null, null)).Value!;
        t.Auth.ChangePassword(s1.Student.Id, s1.Password, "pw"); t.Auth.ChangePassword(s2.Student.Id, s2.Password, "pw");
        var port = FreePort();
        await using var host = await PortalHost.StartAsync(t.Factory, port);
        var c = new Client($"http://127.0.0.1:{port}"); var other = new Client($"http://127.0.0.1:{port}");
        await c.Post("/account/login", new() { ["Identifier"] = "P1", ["Password"] = "pw" }, "/account/login");
        await other.Post("/account/login", new() { ["Identifier"] = "P2", ["Password"] = "pw" }, "/account/login");

        Assert.Equal(HttpStatusCode.NotFound, (await c.Http.GetAsync($"/portal/media/{withPic.Id}")).StatusCode);   // not started yet
        var take = await c.Post($"/portal/start/{exam.Id}", new(), "/portal");
        Assert.Contains("<textarea", take.Html); Assert.Contains($"/portal/media/{withPic.Id}", take.Html);
        Assert.Contains("Section B", take.Html);
        var img = await c.Http.GetAsync($"/portal/media/{withPic.Id}");
        Assert.Equal("image/png", img.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Http.GetAsync($"/portal/media/{withPic.Id}")).StatusCode);   // other student, no attempt

        var qids = Regex.Matches(take.Html, "name=\"q_(\\d+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        // autosave
        var (_, page) = await c.Get(take.Url);
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        var save = await c.Http.PostAsync(take.Url + "/save", new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token, [$"q_{qids[1]}"] = "half written" }));
        Assert.Contains("\"ok\":true", await save.Content.ReadAsStringAsync());
        Assert.Contains("half written", (await c.Get(take.Url)).Html);          // survives a reload

        var done = await c.Post(take.Url, new() { [$"q_{qids[0]}"] = "A", [$"q_{qids[1]}"] = "a full answer" });
        Assert.Contains("Exam completed", done.Html);
        Assert.DoesNotContain("Awaiting marking", done.Html);
        Assert.DoesNotContain("a full answer", done.Html);

        var sheet = new MarkingService(t.Factory).Sheet(int.Parse(take.Url.Split('/').Last()))!;
        new MarkingService(t.Factory).Save(sheet.Attempt.Id, new[] { new MarkEntry(withPic.Id, 8, "Nice") }, true);
        var final = await c.Get(done.Url);
        Assert.Contains("Exam completed", final.Html);
        Assert.DoesNotContain("9 of 11", final.Html); Assert.DoesNotContain("Nice", final.Html);   // marks stay with the teacher
    }
}
