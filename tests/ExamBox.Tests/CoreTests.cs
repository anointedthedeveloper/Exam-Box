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
        Assert.False(t.Auth.CreateAdmin("A", "admin", null, "short").Ok);
        Assert.False(t.Auth.CreateAdmin("A", "admin", null, "onlyletters").Ok);
        Assert.True(t.Auth.CreateAdmin("Ada", "admin", null, "Passw0rdAdmin").Ok);
        Assert.True(t.Auth.HasAdmin());
        Assert.False(t.Auth.CreateAdmin("B", "admin2", null, "Passw0rdAdmin").Ok);
    }

    [Fact]
    public void Authenticate_enforces_role_case_insensitivity_and_lockout()
    {
        using var t = new TempDb();
        t.Auth.CreateAdmin("Ada", "admin", null, "Passw0rdAdmin");
        Assert.NotNull(t.Auth.Authenticate("ADMIN", "Passw0rdAdmin", UserRole.Admin).User);
        Assert.Null(t.Auth.Authenticate("admin", "Passw0rdAdmin", UserRole.Student).User);

        for (var i = 0; i < 5; i++) Assert.Null(t.Auth.Authenticate("admin", "wrong", UserRole.Admin).User);
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
