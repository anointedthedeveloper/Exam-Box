using ExamBox.Data;
using ExamBox.Models;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Services;

public sealed record StudentInput(string FullName, string Username, string? Email, string? Department, bool IsActive = true, string? Password = null);
public sealed record CreatedStudent(User Student, string Password);

public sealed class StudentService(DbFactory factory)
{
    private readonly PasswordVault _vault = new(factory);

    /// <summary>The password the admin set for this student, when it is known.</summary>
    public string? RevealPassword(int id)
    {
        using var db = factory.Create();
        return _vault.Reveal(db.Users.Where(u => u.Id == id && u.Role == UserRole.Student).Select(u => u.PasswordCipher).FirstOrDefault());
    }

    public List<User> List(string? search = null)
    {
        using var db = factory.Create();
        var q = db.Users.AsNoTracking().Include(u => u.Attempts).Where(u => u.Role == UserRole.Student);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u => EF.Functions.Like(u.FullName, $"%{s}%") || EF.Functions.Like(u.Username, $"%{s}%")
                             || (u.Email != null && EF.Functions.Like(u.Email, $"%{s}%")));
        }
        return q.OrderBy(u => u.FullName).ToList();
    }

    /// <summary>Distinct class/department names in use, for pickers.</summary>
    public List<string> Classes()
    {
        using var db = factory.Create();
        return db.Users.Where(u => u.Role == UserRole.Student && u.Department != null && u.Department != "")
            .Select(u => u.Department!).Distinct().OrderBy(d => d).ToList();
    }

    public User? Get(int id)
    {
        using var db = factory.Create();
        return db.Users.AsNoTracking().Include(u => u.Attempts).ThenInclude(a => a.Exam)
            .FirstOrDefault(u => u.Id == id && u.Role == UserRole.Student);
    }

    private static string? Check(StudentInput i)
    {
        if (string.IsNullOrWhiteSpace(i.FullName)) return "Enter the student's full name.";
        if (string.IsNullOrWhiteSpace(i.Username)) return "Enter a student ID.";
        if (i.FullName.Trim().Length > 120) return "Name is too long.";
        if (i.Username.Trim().Length > 60) return "Student ID is too long.";
        if (!string.IsNullOrWhiteSpace(i.Email) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(i.Email.Trim()))
            return "Enter a valid email address.";
        return null;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public OpResult<CreatedStudent> Create(StudentInput i)
    {
        var err = Check(i);
        if (err != null) return OpResult<CreatedStudent>.Fail(err);
        using var db = factory.Create();
        var username = i.Username.Trim(); var email = Clean(i.Email);
        if (db.Users.Any(u => u.Username == username)) return OpResult<CreatedStudent>.Fail("A user with this ID already exists.");
        if (email != null && db.Users.Any(u => u.Email == email)) return OpResult<CreatedStudent>.Fail("A user with this email already exists.");
        var temp = string.IsNullOrEmpty(i.Password) ? Passwords.Generate() : i.Password;
        var s = new User
        {
            FullName = i.FullName.Trim(), Username = username, Email = email, Department = Clean(i.Department),
            IsActive = i.IsActive, Role = UserRole.Student, PasswordHash = Passwords.Hash(temp), PasswordCipher = _vault.Protect(temp),
        };
        db.Users.Add(s);
        db.SaveChanges();
        return OpResult<CreatedStudent>.Success(new CreatedStudent(s, temp));
    }

    /// <summary>Creates many students; rows that fail (duplicate ID, missing name ...) are skipped with the reason and a 1-based row index.</summary>
    public StudentImportResult CreateMany(IReadOnlyList<ImportedStudent> rows, string? defaultClass = null)
    {
        var created = new List<CreatedStudent>(); var skipped = new List<ImportIssue>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var n = 0; n < rows.Count; n++)
        {
            var r = rows[n];
            if (!string.IsNullOrWhiteSpace(r.Username) && !seen.Add(r.Username.Trim()))
            { skipped.Add(new ImportIssue(n + 1, $"{r.Username}: listed twice in the file.")); continue; }
            var res = Create(new StudentInput(r.FullName, r.Username, r.Email, string.IsNullOrWhiteSpace(r.Department) ? defaultClass : r.Department, true, r.Password));
            if (res.Ok) created.Add(res.Value!); else skipped.Add(new ImportIssue(n + 1, $"{(string.IsNullOrWhiteSpace(r.Username) ? r.FullName : r.Username)}: {res.Error}"));
        }
        return new StudentImportResult(created, skipped);
    }

    public OpResult Update(int id, StudentInput i)
    {
        var err = Check(i);
        if (err != null) return OpResult.Fail(err);
        using var db = factory.Create();
        var s = db.Users.FirstOrDefault(u => u.Id == id && u.Role == UserRole.Student);
        if (s == null) return OpResult.Fail("Student not found.");
        var username = i.Username.Trim(); var email = Clean(i.Email);
        if (db.Users.Any(u => u.Id != id && u.Username == username)) return OpResult.Fail("A user with this ID already exists.");
        if (email != null && db.Users.Any(u => u.Id != id && u.Email == email)) return OpResult.Fail("A user with this email already exists.");
        s.FullName = i.FullName.Trim(); s.Username = username; s.Email = email; s.Department = Clean(i.Department); s.IsActive = i.IsActive;
        if (!string.IsNullOrEmpty(i.Password)) { s.PasswordHash = Passwords.Hash(i.Password); s.PasswordCipher = _vault.Protect(i.Password); s.FailedLogins = 0; s.LockoutEnd = null; }
        db.SaveChanges();
        return OpResult.Success();
    }

    /// <summary>Sets a new password chosen by the admin (or a generated one when none is given).</summary>
    public OpResult<string> ResetPassword(int id, string? newPassword = null)
    {
        using var db = factory.Create();
        var s = db.Users.FirstOrDefault(u => u.Id == id && u.Role == UserRole.Student);
        if (s == null) return OpResult<string>.Fail("Student not found.");
        var temp = string.IsNullOrEmpty(newPassword) ? Passwords.Generate() : newPassword;
        s.PasswordHash = Passwords.Hash(temp); s.PasswordCipher = _vault.Protect(temp); s.MustChangePassword = false; s.FailedLogins = 0; s.LockoutEnd = null;
        db.SaveChanges();
        return OpResult<string>.Success(temp);
    }

    public OpResult Delete(int id)
    {
        using var db = factory.Create();
        var s = db.Users.FirstOrDefault(u => u.Id == id && u.Role == UserRole.Student);
        if (s == null) return OpResult.Fail("Student not found.");
        db.Users.Remove(s);
        db.SaveChanges();
        return OpResult.Success();
    }
}

public sealed record ImportedStudent(string FullName, string Username, string? Email, string? Department, string? Password = null);
public sealed record StudentImportResult(List<CreatedStudent> Created, List<ImportIssue> Skipped);

public static class StudentImporter
{
    public static byte[] BuildTemplate()
    {
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.AddWorksheet("Students");
        string[] h = { "Student ID", "First name", "Last name", "Class", "Password", "Email" };
        for (var i = 0; i < h.Length; i++)
        {
            var c = ws.Cell(1, i + 1);
            c.Value = h[i]; c.Style.Font.Bold = true; c.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            c.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#1D4ED8");
        }
        ws.Column(1).Style.NumberFormat.Format = "@";
        var how = wb.AddWorksheet("How to use");
        how.Cell(1, 1).Value = "List one student per row on the \"Students\" sheet, starting directly under the headings.";
        how.Cell(2, 1).Value = "Student ID, First name and Last name are required (a single Full name column also works). Class (for example SS1), Password and Email are optional.";
        how.Cell(3, 1).Value = "Example: SS1/2025/001 | Ada Okafor | SS1";
        how.Cell(4, 1).Value = "Leave Password empty and ExamBox generates one for the student. You can change any password later from the Students page.";
        how.Column(1).Width = 110;
        ws.Column(1).Width = 20; ws.Column(2).Width = 20; ws.Column(3).Width = 20; ws.Column(4).Width = 14; ws.Column(5).Width = 20; ws.Column(6).Width = 30;
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static (List<ImportedStudent> Rows, List<ImportIssue> Issues) Parse(Stream xlsx)
    {
        var rows = new List<ImportedStudent>(); var issues = new List<ImportIssue>();
        ClosedXML.Excel.XLWorkbook wb;
        try { wb = new ClosedXML.Excel.XLWorkbook(xlsx); }
        catch { issues.Add(new ImportIssue(0, "This is not a valid Excel (.xlsx) file.")); return (rows, issues); }
        using (wb)
        {
            var ws = wb.Worksheets.First();
            var used = ws.RangeUsed();
            if (used == null) { issues.Add(new ImportIssue(0, "The sheet is empty.")); return (rows, issues); }
            var map = new Dictionary<string, int>();
            var hr = used.FirstRow().RowNumber();
            for (var c = used.FirstColumn().ColumnNumber(); c <= used.LastColumn().ColumnNumber(); c++)
            {
                var k = new string(ws.Cell(hr, c).GetString().Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
                var key = k switch
                {
                    "studentid" or "id" or "matric" or "matricno" or "matricnumber" or "admissionno" or "regno" or "username" => "id",
                    "fullname" or "name" or "studentname" => "name",
                    "firstname" or "first" or "givenname" or "forename" => "first",
                    "lastname" or "last" or "surname" or "familyname" => "last",
                    "class" or "department" or "dept" or "arm" or "level" => "class",
                    "email" or "emailaddress" => "email",
                    "password" or "pass" or "pin" => "password",
                    _ => null,
                };
                if (key != null && !map.ContainsKey(key)) map[key] = c;
            }
            if (!map.ContainsKey("id") || !(map.ContainsKey("name") || map.ContainsKey("first") || map.ContainsKey("last")))
            {
                issues.Add(new ImportIssue(0, "Could not find the \"Student ID\" and name columns (First name and Last name). Use the ExamBox student template."));
                return (rows, issues);
            }
            string Get(int r, string k) => map.TryGetValue(k, out var c) ? ws.Cell(r, c).GetFormattedString().Trim() : "";
            for (var r = hr + 1; r <= used.LastRow().RowNumber(); r++)
            {
                var id = Get(r, "id"); var name = Get(r, "name");
                if (name.Length == 0) name = (Get(r, "first") + " " + Get(r, "last")).Trim();
                if (id.Length == 0 && name.Length == 0) continue;
                rows.Add(new ImportedStudent(name, id, Get(r, "email"), Get(r, "class"), Get(r, "password")));
                // row numbers are kept on the issues produced at creation time via index
            }
        }
        if (rows.Count == 0 && issues.Count == 0) issues.Add(new ImportIssue(0, "No students were found under the headings."));
        return (rows, issues);
    }

    /// <summary>Excel sheet the teacher prints/cuts so each student gets their first password.</summary>
    public static byte[] CredentialSheet(IEnumerable<CreatedStudent> created, string portalUrl)
    {
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.AddWorksheet("Logins");
        string[] h = { "Student ID", "Full name", "Class", "Password", "Portal" };
        for (var i = 0; i < h.Length; i++) { ws.Cell(1, i + 1).Value = h[i]; ws.Cell(1, i + 1).Style.Font.Bold = true; }
        var r = 2;
        foreach (var c in created)
        {
            ws.Cell(r, 1).Value = c.Student.Username; ws.Cell(r, 2).Value = c.Student.FullName;
            ws.Cell(r, 3).Value = c.Student.Department ?? ""; ws.Cell(r, 4).Value = c.Password; ws.Cell(r, 5).Value = portalUrl;
            r++;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
