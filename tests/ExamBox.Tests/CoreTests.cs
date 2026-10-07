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
        Assert.True(c.Value!.Student.MustChangePassword);
        Assert.False(t.Students.Create(new StudentInput("Dup", "s001", null, null)).Ok);           // ID unique, case-insensitive
        Assert.False(t.Students.Create(new StudentInput("Dup", "S002", "STU@x.com", null)).Ok);    // email unique
        Assert.False(t.Students.Create(new StudentInput("", "S003", null, null)).Ok);
        Assert.False(t.Students.Create(new StudentInput("Bad", "S004", "not-an-email", null)).Ok);

        var login = t.Auth.Authenticate("S001", c.Value.TempPassword, UserRole.Student);
        Assert.NotNull(login.User);

        Assert.True(t.Auth.ChangePassword(login.User!.Id, c.Value.TempPassword, "NewPass123").Ok);
        Assert.True(t.Auth.ChangePassword(login.User.Id, "NewPass123", "abc").Ok);        // short passwords are fine
        Assert.True(t.Auth.ChangePassword(login.User.Id, "abc", "NewPass123").Ok);
        Assert.False(t.Auth.ChangePassword(login.User.Id, "wrong", "NewPass456").Ok);
        Assert.False(t.Auth.Authenticate("S001", c.Value.TempPassword, UserRole.Student).User != null);

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
