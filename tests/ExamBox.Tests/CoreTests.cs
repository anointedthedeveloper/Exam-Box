using Microsoft.EntityFrameworkCore;
using ExamBox.Models;
using ExamBox.Services;
using Xunit;

namespace ExamBox.Tests;

public class CoreTests
{
    [Fact]
    public void Admin_can_only_be_created_once_with_strong_password()
    {
        using var t = new TempDb();
        Assert.False(t.Auth.HasAdmin());
        Assert.False(t.Auth.CreateAdmin("admin", "").Ok);       // password required
        Assert.False(t.Auth.CreateAdmin("  ", "x").Ok);         // username required
        var admin = t.Auth.CreateAdmin("admin", "pw");           // no length/complexity rule
        Assert.True(admin.Ok);
        Assert.Equal("admin", admin.Value!.FullName);            // username doubles as display name
        Assert.True(t.Auth.HasAdmin());
        Assert.False(t.Auth.CreateAdmin("admin2", "Passw0rdAdmin").Ok);
    }

    [Fact]
    public void Authenticate_enforces_role_case_insensitivity_and_lockout()
    {
        using var t = new TempDb();
        t.Auth.CreateAdmin("admin", "Passw0rdAdmin");
        Assert.NotNull(t.Auth.Authenticate("ADMIN", "Passw0rdAdmin", UserRole.Admin).User);
        var wrongPortal = t.Auth.Authenticate("admin", "Passw0rdAdmin", UserRole.Student);
        Assert.Null(wrongPortal.User);
        Assert.Equal("Invalid ID or password.", wrongPortal.Error);     // does not reveal that this is an admin account
        Assert.Equal(t.Auth.Authenticate("nobody", "x", UserRole.Student).Error, wrongPortal.Error);
        t.Students.Create(new StudentInput("Em", "E1", "em@x.com", null));
        var byEmail = t.Auth.Authenticate("em@x.com", "whatever", UserRole.Student);       // sign-in is by ID only
        Assert.Null(byEmail.User); Assert.Equal("Invalid ID or password.", byEmail.Error);

        for (var i = 0; i < 9; i++) Assert.Null(t.Auth.Authenticate("admin", "wrong", UserRole.Admin).User);
        Assert.NotNull(t.Auth.Authenticate("admin", "Passw0rdAdmin", UserRole.Admin).User);   // 9 failures: still allowed, and success resets the count
        AuthResult last = null!;
        for (var i = 0; i < 10; i++) { last = t.Auth.Authenticate("admin", "wrong", UserRole.Admin); Assert.Null(last.User); }
        Assert.NotNull(last.LockedUntil);
        var locked = t.Auth.Authenticate("admin", "Passw0rdAdmin", UserRole.Admin);
        Assert.Null(locked.User);
        Assert.Contains("Too many", locked.Error);
    }

    [Fact]
    public void Student_lifecycle()
    {
        using var t = new TempDb();
        var c = t.Students.Create(new StudentInput("Stu Dent", "S001", "stu@x.com", "CS"));
        Assert.True(c.Ok);
        Assert.False(c.Value!.Student.MustChangePassword);
        Assert.False(t.Students.Create(new StudentInput("Dup", "s001", null, null)).Ok);           // ID unique, case-insensitive
        Assert.False(t.Students.Create(new StudentInput("Dup", "S002", "STU@x.com", null)).Ok);    // email unique
        Assert.False(t.Students.Create(new StudentInput("", "S003", null, null)).Ok);
        Assert.False(t.Students.Create(new StudentInput("Bad", "S004", "not-an-email", null)).Ok);

        var login = t.Auth.Authenticate("S001", c.Value.Password, UserRole.Student);
        Assert.NotNull(login.User);

        Assert.True(t.Auth.ChangePassword(login.User!.Id, c.Value.Password, "NewPass123").Ok);
        Assert.True(t.Auth.ChangePassword(login.User.Id, "NewPass123", "abc").Ok);        // short passwords are fine
        Assert.True(t.Auth.ChangePassword(login.User.Id, "abc", "NewPass123").Ok);
        Assert.False(t.Auth.ChangePassword(login.User.Id, "wrong", "NewPass456").Ok);
        Assert.False(t.Auth.Authenticate("S001", c.Value.Password, UserRole.Student).User != null);

        var reset = t.Students.ResetPassword(login.User.Id);
        Assert.NotNull(t.Auth.Authenticate("S001", reset.Value!, UserRole.Student).User);

        Assert.True(t.Students.Update(login.User.Id, new StudentInput("Stu D", "S001", null, null, false)).Ok);
        Assert.Contains("deactivated", t.Auth.Authenticate("S001", reset.Value!, UserRole.Student).Error);

        Assert.Single(t.Students.List("stu"));
        Assert.Empty(t.Students.List("zzz"));
        Assert.True(t.Students.Delete(login.User.Id).Ok);
        Assert.Null(t.Students.Get(login.User.Id));
    }

    [Fact]
    public void Exam_rules()
    {
        using var t = new TempDb();
        Assert.False(t.Exams.Save(0, "", null, 10, 50).Ok);
        Assert.False(t.Exams.Save(0, "X", null, 0, 50).Ok);
        var e = t.Exams.Save(0, "Math", "d", 10, 50).Value!;
        Assert.False(t.Exams.SetPublished(e.Id, true).Ok);                                   // no questions yet
        Assert.False(t.Exams.SaveQuestion(e.Id, 0, "Q", "a", "b", null, null, "C", 1).Ok);   // C not filled
        Assert.False(t.Exams.SaveQuestion(e.Id, 0, "Q", "a", "b", null, "d", "A", 1).Ok);    // D without C
        Assert.True(t.Exams.SaveQuestion(e.Id, 0, "2+2?", "3", "4", null, null, "B", 2).Ok);
        Assert.True(t.Exams.SetPublished(e.Id, true).Ok);

        var q = t.Exams.Get(e.Id)!.Questions.Single();
        Assert.True(t.Exams.DeleteQuestion(e.Id, q.Id).Ok);
        Assert.False(t.Exams.Get(e.Id)!.IsPublished);                                        // auto-unpublished when empty

        t.Exams.SaveQuestion(e.Id, 0, "again", "a", "b", null, null, "A", 1);
        var s = t.Students.Create(new StudentInput("S", "S1", null, null)).Value!.Student;
        using (var db = t.Factory.Create()) { db.Attempts.Add(new Attempt { ExamId = e.Id, StudentId = s.Id }); db.SaveChanges(); }
        Assert.True(t.Exams.IsLocked(e.Id));
        Assert.False(t.Exams.SaveQuestion(e.Id, 0, "late", "a", "b", null, null, "A", 1).Ok);
    }

    [Fact]
    public void Dashboard_reflects_real_data()
    {
        using var t = new TempDb();
        Assert.Equal(0, new DashboardService(t.Factory).Get().Students);
        t.Students.Create(new StudentInput("A", "A1", null, null));
        t.Exams.Save(0, "E", null, 5, 50);
        var d = new DashboardService(t.Factory).Get();
        Assert.Equal(1, d.Students); Assert.Equal(1, d.Exams); Assert.Equal(0, d.PublishedExams);
    }
}

public class RecoveryTests
{
    [Fact]
    public void Reset_admin_password_unlocks_and_replaces_password()
    {
        using var t = new TempDb();
        Assert.False(t.Auth.ResetAdminPassword("x").Ok);                       // no admin yet
        t.Auth.CreateAdmin("admin", "old");
        for (var i = 0; i < 10; i++) t.Auth.Authenticate("admin", "bad", UserRole.Admin);   // lock it
        Assert.False(t.Auth.ResetAdminPassword("").Ok);
        var r = t.Auth.ResetAdminPassword("fresh");
        Assert.True(r.Ok); Assert.Equal("admin", r.Value);
        Assert.NotNull(t.Auth.Authenticate("admin", "fresh", UserRole.Admin).User);
        Assert.Null(t.Auth.Authenticate("admin", "old", UserRole.Admin).User);
    }
}

public class AttemptWarningTests
{
    [Fact]
    public void Warns_when_few_attempts_are_left()
    {
        using var t = new TempDb();
        t.Auth.CreateAdmin("admin", "Passw0rdAdmin");
        for (var i = 0; i < 6; i++) Assert.DoesNotContain("left", t.Auth.Authenticate("admin", "bad", UserRole.Admin).Error);
        var r7 = t.Auth.Authenticate("admin", "bad", UserRole.Admin);
        Assert.Equal(3, r7.AttemptsLeft);
        Assert.Contains("3 attempts left", r7.Error);
        t.Auth.Authenticate("admin", "bad", UserRole.Admin);
        var r9 = t.Auth.Authenticate("admin", "bad", UserRole.Admin);
        Assert.Contains("1 attempt left", r9.Error);
        var r10 = t.Auth.Authenticate("admin", "bad", UserRole.Admin);
        Assert.Contains("locked", r10.Error);
        // wrong-portal attempts never count toward the lock
        t.Students.Create(new StudentInput("S", "S1", null, null));
    }
}

public class ReportTests
{
    private static (TempDb t, Exam exam) Seed(params (int score, DateTime when)[] attempts)
    {
        var t = new TempDb();
        var exam = t.Exams.Save(0, "Math", null, 30, 50).Value!;
        var i = 0;
        foreach (var (score, when) in attempts)
        {
            var s = t.Students.Create(new StudentInput($"Student {i}", $"S{i}", null, null)).Value!.Student; i++;
            using var db = t.Factory.Create();
            db.Attempts.Add(new Attempt { ExamId = exam.Id, StudentId = s.Id, Score = score, TotalMarks = 100, StartedAt = when, SubmittedAt = when });
            db.SaveChanges();
        }
        return (t, exam);
    }

    [Fact]
    public void Report_numbers_grades_and_trend()
    {
        var now = DateTime.UtcNow;
        var (t, exam) = Seed((95, now), (85, now), (72, now.AddMonths(-1)), (61, now.AddMonths(-2)), (40, now.AddMonths(-2)));
        using var _ = t;
        var r = new ReportService(t.Factory).Build();
        Assert.Equal(5, r.Attempts);
        Assert.Equal(70.6, r.AvgPercent);
        Assert.Equal(80, r.PassRate);                                  // 4 of 5 reach the 50% pass mark
        Assert.Equal(95, r.HighPercent); Assert.Equal(40, r.LowPercent);
        Assert.Equal(new[] { 1, 1, 1, 1, 1 }, r.GradeCounts);          // A B C D E
        Assert.Equal(6, r.Trend.Count);
        Assert.Equal(2, r.Trend[^1].Passed);                            // this month: 95 and 85
        Assert.Equal(1, r.Trend[^3].Failed);                            // two months ago: the 40
        Assert.Single(r.Exams); Assert.Equal(5, r.Exams[0].Attempts);
        Assert.Equal("Student 0", r.TopStudents[0].Name);
    }

    [Fact]
    public void Filters_by_period_and_exam_and_exports()
    {
        var now = DateTime.UtcNow;
        var (t, exam) = Seed((90, now), (50, now.AddDays(-40)));
        using var _ = t;
        var svc = new ReportService(t.Factory);
        Assert.Equal(1, svc.Build(null, now.AddDays(-30)).Attempts);   // last 30 days only
        Assert.Equal(2, svc.Build(exam.Id).Attempts);
        Assert.Equal(0, svc.Build(exam.Id + 99).Attempts);
        Assert.Equal(2, svc.Export().Count);
        Assert.Equal(1, svc.Export(null, now.AddDays(-30)).Count);
        Assert.Equal(new double[8].Length, new DashboardService(t.Factory).Get().StudentsByWeek.Length);
    }

    [Fact]
    public void Empty_database_gives_zeroes_not_errors()
    {
        using var t = new TempDb();
        var r = new ReportService(t.Factory).Build();
        Assert.Equal(0, r.Attempts); Assert.Equal(0, r.PassRate); Assert.Equal(6, r.Trend.Count);
        Assert.All(r.GradeCounts, c => Assert.Equal(0, c));
    }

    [Fact]
    public void Backup_is_a_usable_copy()
    {
        using var t = new TempDb();
        t.Students.Create(new StudentInput("A", "A1", null, null));
        var dir = Path.Combine(Path.GetTempPath(), "exambox-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var target = Path.Combine(dir, "exambox.db");
            t.Factory.BackupTo(target);
            var copy = new ExamBox.Data.DbFactory(dir);
            Assert.Single(new StudentService(copy).List());
            Assert.Throws<InvalidOperationException>(() => t.Factory.BackupTo(t.Factory.DbPath));
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); try { Directory.Delete(dir, true); } catch { } }
    }
}

public class ProductionTests
{
    [Fact]
    public void Old_v1_database_is_upgraded_in_place_without_losing_data()
    {
        var dir = Path.Combine(Path.GetTempPath(), "exambox-up-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var f = new ExamBox.Data.DbFactory(dir);
            using (var c = new Microsoft.Data.Sqlite.SqliteConnection(f.ConnectionString))
            {
                c.Open();
                foreach (var sql in new[]
                {
                    "CREATE TABLE Users (Id INTEGER PRIMARY KEY AUTOINCREMENT, FullName TEXT NOT NULL, Username TEXT NOT NULL COLLATE NOCASE, Email TEXT NULL COLLATE NOCASE, Department TEXT NULL, PasswordHash TEXT NOT NULL, Role TEXT NOT NULL, IsActive INTEGER NOT NULL, MustChangePassword INTEGER NOT NULL, FailedLogins INTEGER NOT NULL, LockoutEnd TEXT NULL, CreatedAt TEXT NOT NULL, LastLoginAt TEXT NULL)",
                    "CREATE TABLE Exams (Id INTEGER PRIMARY KEY AUTOINCREMENT, Title TEXT NOT NULL, Description TEXT NULL, DurationMinutes INTEGER NOT NULL, PassMarkPercent INTEGER NOT NULL, IsPublished INTEGER NOT NULL, CreatedAt TEXT NOT NULL)",
                    "CREATE TABLE Questions (Id INTEGER PRIMARY KEY AUTOINCREMENT, ExamId INTEGER NOT NULL, Text TEXT NOT NULL, OptionA TEXT NOT NULL, OptionB TEXT NOT NULL, OptionC TEXT NULL, OptionD TEXT NULL, CorrectOption TEXT NOT NULL, Marks INTEGER NOT NULL)",
                    "CREATE TABLE Attempts (Id INTEGER PRIMARY KEY AUTOINCREMENT, ExamId INTEGER NOT NULL, StudentId INTEGER NOT NULL, StartedAt TEXT NOT NULL, SubmittedAt TEXT NULL, Score INTEGER NOT NULL, TotalMarks INTEGER NOT NULL)",
                    "CREATE TABLE Answers (Id INTEGER PRIMARY KEY AUTOINCREMENT, AttemptId INTEGER NOT NULL, QuestionId INTEGER NOT NULL, Selected TEXT NULL)",
                    "INSERT INTO Users VALUES (1,'Old Student','S1',NULL,NULL,'x','Student',1,1,0,NULL,'2025-01-01 00:00:00',NULL)",
                    "INSERT INTO Exams VALUES (1,'Old exam',NULL,30,50,1,'2025-01-01 00:00:00')",
                    "INSERT INTO Questions VALUES (1,1,'2+2?','3','4',NULL,NULL,'B',2)",
                    "INSERT INTO Questions VALUES (2,1,'3+3?','5','6',NULL,NULL,'B',2)",
                    "INSERT INTO Attempts VALUES (1,1,1,'2025-01-02 00:00:00','2025-01-02 00:10:00',4,4)",
                    "INSERT INTO Answers VALUES (1,1,1,'B')",
                })
                {
                    using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery();
                }
            }
            f.Initialize();
            f.Initialize(); // second run is a no-op
            var exams = new ExamService(f);
            var e = exams.Get(1)!;
            Assert.Equal(new[] { 1, 2 }, e.Questions.Select(q => q.Id));
            Assert.All(e.Questions, q => Assert.Equal(ExamBox.Models.QuestionType.Objective, q.Type));
            Assert.True(e.ShowCorrectAnswers);
            using var db = f.Create();
            var a = db.Attempts.Single();
            Assert.Equal(4, a.ObjectiveScore); Assert.Equal(4, a.Score); Assert.False(a.PendingMarking);
            Assert.Equal("B", db.Answers.Single().Selected);
            Assert.False(db.Users.Single().MustChangePassword);   // forced password changes no longer exist
            Assert.True(exams.SaveQuestion(1, 0, new QuestionInput(ExamBox.Models.QuestionType.Theory, "3a", "Explain.", 5)).Ok == false); // locked: attempt exists
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Template_roundtrip_and_import_rules()
    {
        // the downloadable template parses cleanly (example sheet is ignored, questions sheet is empty -> clear message)
        var blank = QuestionImporter.Parse(new MemoryStream(QuestionImporter.BuildTemplate()));
        Assert.Contains(blank.Issues, i => i.Message.Contains("No questions") || i.Message.Contains("empty"));

        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.AddWorksheet("Questions");
        string[] h = { "No", "Type", "Question", "A", "B", "C", "D", "E", "Correct", "Marks", "Image", "Model answer" };
        for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        void Row(int r, params object[] v) { for (var i = 0; i < v.Length; i++) ws.Cell(r, i + 1).Value = v[i]?.ToString() ?? ""; }
        Row(2, "1", "OBJ", "2+2?", "3", "4", "", "", "", "B", 2, "", "");
        Row(3, "2", "mcq", "Pick the shape", "x", "y", "z", "", "", "c", "", "yes", "");
        Row(4, "3", "THEORY", "Passage text", "", "", "", "", "", "", 0, "", "");
        Row(5, "3a", "ESSAY", "Explain", "", "", "", "", "", "", 5, "map.png", "Because.");
        Row(6, "4", "OBJ", "bad one", "only a", "", "", "", "", "A", 1, "", "");     // B missing
        Row(7, "5", "WHAT", "bad type", "a", "b", "", "", "", "A", 1, "", "");
        Row(8, "6", "OBJ", "bad key", "a", "b", "", "", "", "D", 1, "", "");          // D not filled
        Row(9, "7", "OBJ", "bad marks", "a", "b", "", "", "", "A", "abc", "", "");
        using var ms = new MemoryStream(); wb.SaveAs(ms); ms.Position = 0;
        var res = QuestionImporter.Parse(ms);
        Assert.Equal(4, res.Questions.Count);
        Assert.Equal(new[] { 6, 7, 8, 9 }, res.Issues.Where(i => !i.IsWarning).Select(i => i.Row).ToArray());
        Assert.Equal("C", res.Questions[1].CorrectOption);
        Assert.Equal(1, res.Questions[1].Marks);              // default 1 for OBJ
        Assert.True(res.Questions[1].ImageRequired);
        Assert.Equal(QuestionType.Theory, res.Questions[2].Type);
        Assert.Equal(0, res.Questions[2].Marks);              // passage
        Assert.Equal("3a", res.Questions[3].Number);
        Assert.True(res.Questions[3].ImageRequired);          // map.png not loaded -> must be attached
        Assert.Equal(2, res.NeedImages);
        Assert.Contains(res.Issues, i => i.IsWarning && i.Row == 5);
    }

    [Fact]
    public void Images_folder_resolves_file_names()
    {
        var dir = Path.Combine(Path.GetTempPath(), "exambox-img-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
            File.WriteAllBytes(Path.Combine(dir, "pic.png"), png);
            using var wb = new ClosedXML.Excel.XLWorkbook();
            var ws = wb.AddWorksheet("Questions");
            string[] h = { "No", "Type", "Question", "A", "B", "Correct", "Marks", "Image" };
            for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
            string[] r2 = { "1", "OBJ", "See pic", "a", "b", "A", "1", "pic.png" };
            string[] r3 = { "2", "OBJ", "Traversal", "a", "b", "A", "1", "../../etc/passwd" };
            for (var i = 0; i < r2.Length; i++) { ws.Cell(2, i + 1).Value = r2[i]; ws.Cell(3, i + 1).Value = r3[i]; }
            using var ms = new MemoryStream(); wb.SaveAs(ms); ms.Position = 0;
            var res = QuestionImporter.Parse(ms, dir);
            Assert.True(res.Questions[0].HasImage); Assert.Equal("image/png", res.Questions[0].ImageType);
            Assert.False(res.Questions[1].HasImage); Assert.True(res.Questions[1].MissingImage);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void Launch_rules_duplicate_and_images()
    {
        using var t = new TempDb();
        var e = t.Exams.Save(0, "SS1 English First Term", null, 40, 50, "SS1").Value!;
        Assert.False(t.Exams.Launch(e.Id, null, null, null).Ok);                       // no questions
        t.Exams.SaveQuestion(e.Id, 0, new QuestionInput(QuestionType.Objective, null, "Q?", 1, "a", "b", Correct: "A", ImageRequired: true));
        var q = t.Exams.Get(e.Id)!.Questions.Single();
        Assert.False(t.Exams.Launch(e.Id, null, null, null).Ok);                       // picture missing
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        Assert.False(t.Exams.SetImage(e.Id, q.Id, new byte[] { 1, 2, 3 }, "image/png").Ok);       // not an image
        Assert.True(t.Exams.SetImage(e.Id, q.Id, png, "image/png").Ok);
        var now = DateTime.UtcNow;
        Assert.False(t.Exams.Launch(e.Id, now.AddDays(2), now.AddDays(1), null).Ok);   // closes before opens
        Assert.True(t.Exams.Launch(e.Id, now.AddDays(2), now.AddDays(3), "SS1").Ok);
        Assert.Equal(ExamState.Scheduled, t.Exams.Get(e.Id)!.StateAt(now));
        var copy = t.Exams.Duplicate(e.Id).Value!;
        var c = t.Exams.Get(copy.Id)!;
        Assert.False(c.IsPublished); Assert.EndsWith("(copy)", c.Title); Assert.True(c.Questions.Single().HasImage); Assert.Equal("SS1", c.ForDepartment);
        // reorder
        t.Exams.SaveQuestion(copy.Id, 0, new QuestionInput(QuestionType.Theory, "2", "Essay", 5));
        var ids = t.Exams.Get(copy.Id)!.Questions.Select(x => x.Id).ToList();
        t.Exams.MoveQuestion(copy.Id, ids[1], -1);
        Assert.Equal(new[] { ids[1], ids[0] }, t.Exams.Get(copy.Id)!.Questions.Select(x => x.Id));
    }

    [Fact]
    public void Mixed_exam_flow_with_theory_marking()
    {
        using var t = new TempDb();
        var att = new AttemptService(t.Factory); var mark = new MarkingService(t.Factory);
        var e = t.Exams.Save(0, "Mixed", null, 30, 50).Value!;
        t.Exams.SaveQuestion(e.Id, 0, "2+2?", "3", "4", null, null, "B", 2);
        t.Exams.Theory(e.Id, "Passage", 0, "3");
        t.Exams.Theory(e.Id, "Explain A", 5, "3a");
        t.Exams.Theory(e.Id, "Explain B", 5, "3b");
        Assert.True(t.Exams.Launch(e.Id, null, null, null).Ok);
        var s = t.Students.Create(new StudentInput("Stu", "S9", null, null)).Value!.Student;
        var aid = att.Start(s.Id, e.Id).Value;
        var take = att.GetTake(s.Id, aid)!;
        Assert.Equal(QuestionType.Objective, take.Questions[0].Type);
        var qs = take.Questions;
        var draft = new Dictionary<int, string> { [qs[2].Id] = "my answer a" };
        Assert.True(att.SaveDraft(s.Id, aid, draft).Ok);
        Assert.Equal("my answer a", att.GetTake(s.Id, aid)!.Saved[qs[2].Id].Text);
        var final = new Dictionary<int, string> { [qs[0].Id] = "B", [qs[2].Id] = "my answer a", [qs[3].Id] = "my answer b" };
        Assert.True(att.Submit(s.Id, aid, final).Ok);
        var r = att.GetResult(s.Id, aid)!;
        Assert.True(r.PendingMarking); Assert.Equal(2, r.ObjectiveScore); Assert.Equal(12, r.TotalMarks);
        Assert.Equal(0, new ReportService(t.Factory).Build().Attempts);                  // not final yet
        Assert.Equal(1, new DashboardService(t.Factory).Get().AwaitingMarking);
        Assert.Single(mark.Pending());
        var sheet = mark.Sheet(aid)!;
        Assert.Equal(2, sheet.Items.Count);
        var a1 = sheet.Items[0].Question.Id; var a2 = sheet.Items[1].Question.Id;
        Assert.False(mark.Save(aid, new[] { new MarkEntry(a1, 6, null) }, false).Ok);      // above max
        Assert.True(mark.Save(aid, new[] { new MarkEntry(a1, 4, "good") }, false).Ok);
        Assert.False(mark.Save(aid, Array.Empty<MarkEntry>(), true).Ok);                 // b unmarked
        Assert.True(mark.Save(aid, new[] { new MarkEntry(a2, 3, null) }, true).Ok);
        r = att.GetResult(s.Id, aid)!;
        Assert.False(r.PendingMarking); Assert.Equal(9, r.Score); Assert.Equal(7, r.TheoryScore);
        Assert.Equal(1, new ReportService(t.Factory).Build().Attempts);
        Assert.Equal(0, mark.PendingCount());
    }

    [Fact]
    public void Visibility_by_class_and_schedule_and_expiry()
    {
        using var t = new TempDb();
        var att = new AttemptService(t.Factory);
        var ss1 = t.Students.Create(new StudentInput("A", "A1", null, "SS1")).Value!.Student;
        var ss2 = t.Students.Create(new StudentInput("B", "B1", null, "SS2")).Value!.Student;
        var e = t.Exams.Save(0, "Class exam", null, 10, 50).Value!;
        t.Exams.SaveQuestion(e.Id, 0, "Q", "a", "b", null, null, "A", 1);
        t.Exams.Launch(e.Id, null, DateTime.UtcNow.AddHours(1), "ss1");
        Assert.Single(att.Home(ss1.Id).Available);
        Assert.Empty(att.Home(ss2.Id).Available);
        Assert.False(att.Start(ss2.Id, e.Id).Ok);
        var later = t.Exams.Save(0, "Later", null, 10, 50).Value!;
        t.Exams.SaveQuestion(later.Id, 0, "Q", "a", "b", null, null, "A", 1);
        t.Exams.Launch(later.Id, DateTime.UtcNow.AddDays(2), null, null);
        var home = att.Home(ss1.Id);
        Assert.Single(home.Upcoming); Assert.False(att.Start(ss1.Id, later.Id).Ok);
        // an attempt whose window shut is collected on its own, using saved answers
        var aid = att.Start(ss1.Id, e.Id).Value;
        using (var db = t.Factory.Create()) db.Exams.Where(x => x.Id == e.Id).ExecuteUpdate(s => s.SetProperty(x => x.ClosesAt, DateTime.UtcNow.AddSeconds(-1)));
        Assert.Equal(1, att.CollectExpired());
        Assert.NotNull(att.GetResult(ss1.Id, aid));
    }

    [Fact]
    public void Student_import_creates_skips_and_exports()
    {
        using var t = new TempDb();
        t.Students.Create(new StudentInput("Existing", "E1", null, null));
        var rows = new List<ImportedStudent> { new("New One", "N1", "", "SS1"), new("Dup", "E1", "", ""), new("", "N2", "", ""), new("Twice", "N1", "", "") };
        var res = t.Students.CreateMany(rows);
        Assert.Single(res.Created); Assert.Equal(3, res.Skipped.Count);
        var sheet = StudentImporter.CredentialSheet(res.Created, "http://x:5000");
        Assert.True(sheet.Length > 100);
        var (parsed, issues) = StudentImporter.Parse(new MemoryStream(StudentImporter.BuildTemplate()));
        Assert.Single(issues); Assert.Empty(parsed);
    }

    [Fact]
    public void Admin_chooses_student_passwords()
    {
        using var t = new TempDb();
        var c = t.Students.Create(new StudentInput("Ada", "A1", null, "SS1", true, "mypass")).Value!;
        Assert.Equal("mypass", c.Password);
        Assert.NotNull(t.Auth.Authenticate("A1", "mypass", UserRole.Student).User);
        var gen = t.Students.Create(new StudentInput("Bola", "B1", null, null)).Value!;        // no password given: one is generated
        Assert.False(string.IsNullOrEmpty(gen.Password));
        Assert.NotNull(t.Auth.Authenticate("B1", gen.Password, UserRole.Student).User);
        Assert.True(t.Students.Update(c.Student.Id, new StudentInput("Ada", "A1", null, "SS1", true, "newer")).Ok);   // edit dialog sets a new one
        Assert.Null(t.Auth.Authenticate("A1", "mypass", UserRole.Student).User);
        Assert.NotNull(t.Auth.Authenticate("A1", "newer", UserRole.Student).User);
        Assert.True(t.Students.Update(c.Student.Id, new StudentInput("Ada O", "A1", null, "SS1", true, "")).Ok);      // empty = keep
        Assert.NotNull(t.Auth.Authenticate("A1", "newer", UserRole.Student).User);
        Assert.Equal("newer", t.Students.RevealPassword(c.Student.Id));
        Assert.Equal("typed", t.Students.ResetPassword(c.Student.Id, "typed").Value);
        Assert.Equal("typed", t.Students.RevealPassword(c.Student.Id));
        Assert.NotNull(t.Auth.Authenticate("A1", "typed", UserRole.Student).User);
    }

    [Fact]
    public void Student_import_uses_password_column_when_given()
    {
        using var t = new TempDb();
        var res = t.Students.CreateMany(new List<ImportedStudent> { new("One", "I1", "", "SS1", "pw-one"), new("Two", "I2", "", "SS1", "") });
        Assert.Equal("pw-one", res.Created[0].Password);
        Assert.NotNull(t.Auth.Authenticate("I1", "pw-one", UserRole.Student).User);
        Assert.False(string.IsNullOrEmpty(res.Created[1].Password));
        var (rows, issues) = StudentImporter.Parse(new MemoryStream(MakeSheet()));
        Assert.Empty(issues); Assert.Equal("Ada Okafor", rows[0].FullName); Assert.Equal("Bola Ade", rows[1].FullName);
    }

    private static byte[] MakeSheet()
    {
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.AddWorksheet("Students");
        string[] h = { "Student ID", "First name", "Last name", "Class" };
        for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        ws.Cell(2, 1).Value = "S1"; ws.Cell(2, 2).Value = "Ada"; ws.Cell(2, 3).Value = "Okafor"; ws.Cell(2, 4).Value = "SS1";
        ws.Cell(3, 1).Value = "S2"; ws.Cell(3, 2).Value = "Bola"; ws.Cell(3, 3).Value = "Ade";
        using var ms = new MemoryStream(); wb.SaveAs(ms); return ms.ToArray();
    }

    [Fact]
    public void Admin_can_pause_resume_and_edit_time()
    {
        using var t = new TempDb();
        var att = new AttemptService(t.Factory); var live = new LiveService(t.Factory);
        var e = t.Exams.Save(0, "Timed", null, 30, 50).Value!;
        t.Exams.SaveQuestion(e.Id, 0, "Q", "a", "b", null, null, "A", 1);
        t.Exams.Launch(e.Id, null, null, null);
        var s = t.Students.Create(new StudentInput("S", "T1", null, null, true, "pw")).Value!.Student;
        var aid = att.Start(s.Id, e.Id).Value;
        var before = att.GetTake(s.Id, aid)!.SecondsLeft;
        Assert.InRange(before, 1790, 1800);

        Assert.True(live.Pause(aid).Ok);
        Assert.True(live.List(e.Id).Single().Paused);
        using (var db = t.Factory.Create())   // pretend 10 minutes passed while paused
            { var row = db.Attempts.Single(a => a.Id == aid); row.PausedAt = row.PausedAt!.Value.AddMinutes(-10); row.StartedAt = row.StartedAt.AddMinutes(-10); db.SaveChanges(); }
        var frozen = att.GetTake(s.Id, aid)!;
        Assert.True(frozen.Paused); Assert.InRange(frozen.SecondsLeft, before - 5, before + 1);   // clock did not run
        Assert.False(att.SaveDraft(s.Id, aid, new Dictionary<int, string>()).Ok);                  // autosave refused while paused
        Assert.Equal(0, att.CollectExpired());

        Assert.True(att.Resume(s.Id, aid).Ok);                                                   // student resumes
        var running = att.GetTake(s.Id, aid)!;
        Assert.False(running.Paused); Assert.InRange(running.SecondsLeft, before - 5, before + 1);

        Assert.True(live.SetMinutesLeft(aid, 5).Ok);                                             // admin edits the time
        Assert.InRange(att.GetTake(s.Id, aid)!.SecondsLeft, 295, 301);
        Assert.True(live.SetMinutesLeft(aid, 45).Ok);                                            // and can extend beyond the original
        Assert.InRange(att.GetTake(s.Id, aid)!.SecondsLeft, 2695, 2701);
        Assert.False(live.SetMinutesLeft(aid, -1).Ok);

        live.Pause(aid); Assert.True(live.SetMinutesLeft(aid, 10).Ok);                            // editing while paused works too
        Assert.InRange(att.GetTake(s.Id, aid)!.SecondsLeft, 595, 601);
        Assert.True(live.Resume(aid).Ok);
        Assert.False(att.GetTake(s.Id, aid)!.Paused);
        Assert.Empty(live.List(e.Id).Where(r => r.Paused));
    }

    [Fact]
    public void Excel_import_keeps_maths_and_pasted_pictures()
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.AddWorksheet("Questions");
        string[] h = { "No", "Type", "Question", "A", "B", "Correct", "Marks", "Image" };
        for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        string[] r2 = { "1", "OBJ", "Find x\u00B2 + \u221A16 \u00F7 2 when x = 3", "11", "9", "A", "1", "" };
        string[] r3 = { "2", "OBJ", "Study the triangle", "a", "b", "B", "1", "" };
        for (var i = 0; i < r2.Length; i++) { ws.Cell(2, i + 1).Value = r2[i]; ws.Cell(3, i + 1).Value = r3[i]; }
        ws.AddPicture(new MemoryStream(png)).MoveTo(ws.Cell(3, 8));          // picture pasted into the Image cell of row 3
        using var ms = new MemoryStream(); wb.SaveAs(ms); ms.Position = 0;
        var res = QuestionImporter.Parse(ms);
        Assert.False(res.HasErrors);
        Assert.Contains("x\u00B2 + \u221A16 \u00F7 2", res.Questions[0].Text);                  // symbols survive
        Assert.False(res.Questions[0].HasImage);
        Assert.True(res.Questions[1].HasImage); Assert.Equal("image/png", res.Questions[1].ImageType);
        Assert.Equal(0, res.NeedImages);
    }

    [Fact]
    public void Results_export_to_excel_and_pdf()
    {
        using var t = new TempDb();
        var att = new AttemptService(t.Factory);
        var e = t.Exams.Save(0, "Maths Mid-term", null, 30, 50).Value!;
        t.Exams.SaveQuestion(e.Id, 0, "2+2?", "3", "4", null, null, "B", 2);
        t.Exams.Launch(e.Id, null, null, null);
        var s = t.Students.Create(new StudentInput("Ada Okafor", "X1", null, "SS1", true, "pw")).Value!.Student;
        var aid = att.Start(s.Id, e.Id).Value;
        att.Submit(s.Id, aid, new Dictionary<int, string> { [t.Exams.Get(e.Id)!.Questions[0].Id] = "B" });
        var rows = new ReportService(t.Factory).ExportRows(e.Id);
        Assert.Single(rows); Assert.Equal("Pass", rows[0].Outcome); Assert.Equal("100%", rows[0].Percent); Assert.Equal("A", rows[0].Grade);
        var xlsx = ResultsExporter.Xlsx("Maths Mid-term", rows, false);
        using (var wb = new ClosedXML.Excel.XLWorkbook(new MemoryStream(xlsx)))
            Assert.Equal("Ada Okafor", wb.Worksheet(1).Cell(5, 2).GetString());
        var pdf = ResultsExporter.Pdf("Greenfield", "Maths Mid-term", "All results", new[] { ("Submissions", "1") }, rows, false);
        Assert.True(pdf.Length > 1000); Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Classes_students_exams_presence_and_log()
    {
        using var t = new TempDb();
        var classes = new ClassService(t.Factory); var log = new ActivityLog(t.Factory); var presence = new PresenceService(t.Factory);
        Assert.False(classes.Create("  ").Ok);
        Assert.True(classes.Create("SS1").Ok);
        Assert.False(classes.Create("ss1").Ok);                                   // names are unique, case-insensitive
        var s = t.Students.Create(new StudentInput("Ada", "C1", null, "SS1", true, "pw")).Value!.Student;
        t.Students.Create(new StudentInput("Bola", "C2", null, "JSS2", true, "pw"));            // an unknown class is created on the fly
        Assert.Equal(new[] { "JSS2", "SS1" }, classes.Names());
        var e = t.Exams.Save(0, "Class exam", null, 10, 50, "ss1").Value!;
        Assert.Equal("SS1", t.Exams.Get(e.Id)!.ForDepartment);                     // stored with the class's own spelling
        var row = classes.List().Single(c => c.Name == "SS1");
        Assert.Equal(1, row.Students); Assert.Equal(1, row.Exams);

        Assert.False(classes.Delete(row.Id).Ok);                                   // still has a student
        Assert.True(classes.Rename(row.Id, "SS One").Ok);
        Assert.Equal("SS One", t.Students.Get(s.Id)!.Department);                  // students and exams follow the rename
        Assert.Equal("SS One", t.Exams.Get(e.Id)!.ForDepartment);
        Assert.False(classes.Rename(row.Id, "JSS2").Ok);                           // name taken
        var empty = classes.Create("Empty").Value!;
        Assert.True(classes.Delete(empty.Id).Ok);

        Assert.Empty(presence.Online());
        presence.Touch(s.Id);
        var on = presence.Online().Single(); Assert.Equal("C1", on.Code); Assert.Equal(1, presence.OnlineCount());

        t.Auth.Authenticate("C1", "wrong", UserRole.Student);
        t.Auth.Authenticate("C1", "pw", UserRole.Student);
        var entries = log.List();
        Assert.Contains(entries, x => x.Kind == "failed" && x.Actor == "C1");
        Assert.Contains(entries, x => x.Kind == "signin" && x.Actor == "C1");
        Assert.Contains(entries, x => x.Kind == "admin" && x.Subject == "Student added");
        Assert.Single(log.List("failed")); Assert.NotEmpty(log.List(search: "Ada"));
    }

    [Fact]
    public void Old_database_gets_classes_from_existing_students()
    {
        var dir = Path.Combine(Path.GetTempPath(), "exambox-cls-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            var f = new ExamBox.Data.DbFactory(dir);
            f.Initialize();
            using (var db = f.Create())
            {
                db.Database.ExecuteSqlRaw("INSERT INTO Users (FullName, Username, Department, PasswordHash, Role, IsActive, MustChangePassword, FailedLogins, CreatedAt, SessionVersion) VALUES ('Old','O1','SS3','x','Student',1,0,0,'2025-01-01',0)");
                db.Database.ExecuteSqlRaw("ALTER TABLE Users DROP COLUMN LastSeenAt"); db.Database.ExecuteSqlRaw("DROP TABLE Classes"); db.Database.ExecuteSqlRaw("DROP TABLE Activity"); db.Database.ExecuteSqlRaw("PRAGMA user_version = 6");
            }
            f.Initialize();                                                         // upgrade 6 -> 7
            Assert.Equal(new[] { "SS3" }, new ClassService(f).Names());
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); try { Directory.Delete(dir, true); } catch { } }
    }
}
