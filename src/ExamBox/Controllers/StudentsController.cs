using ExamBox.Data;
using ExamBox.Models;
using ExamBox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExamBox.Controllers;

[Authorize(Roles = "Admin"), Route("admin/students")]
public class StudentsController(AppDb db) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? q)
    {
        var query = db.Users.Where(u => u.Role == UserRole.Student);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(u => EF.Functions.Like(u.FullName, $"%{q}%") || EF.Functions.Like(u.Username, $"%{q}%")
                                     || (u.Email != null && EF.Functions.Like(u.Email, $"%{q}%")));
        }
        ViewBag.Q = q;
        return View(await query.OrderBy(u => u.FullName).ToListAsync());
    }

    [HttpGet("create")]
    public IActionResult Create() => View("Form", new StudentFormVm());

    [HttpPost("create")]
    public async Task<IActionResult> Create(StudentFormVm vm)
    {
        if (!ModelState.IsValid) return View("Form", vm);
        var username = vm.Username.Trim();
        if (await db.Users.AnyAsync(u => u.Username == username))
        {
            ModelState.AddModelError(nameof(vm.Username), "A user with this ID already exists.");
            return View("Form", vm);
        }
        var email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim();
        if (email != null && await db.Users.AnyAsync(u => u.Email == email))
        {
            ModelState.AddModelError(nameof(vm.Email), "A user with this email already exists.");
            return View("Form", vm);
        }
        var temp = Passwords.Generate();
        var s = new User
        {
            FullName = vm.FullName.Trim(), Username = username, Email = email,
            Department = string.IsNullOrWhiteSpace(vm.Department) ? null : vm.Department.Trim(),
            IsActive = vm.IsActive, Role = UserRole.Student,
            PasswordHash = Passwords.Hash(temp), MustChangePassword = true,
        };
        db.Users.Add(s);
        await db.SaveChangesAsync();
        TempData["TempPassword"] = temp;
        TempData["TempFor"] = s.Username;
        return RedirectToAction(nameof(Details), new { id = s.Id });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var s = await db.Users.Include(u => u.Attempts).ThenInclude(a => a.Exam)
            .FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Student);
        return s == null ? NotFound() : View(s);
    }

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var s = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Student);
        if (s == null) return NotFound();
        return View("Form", new StudentFormVm { Id = s.Id, FullName = s.FullName, Username = s.Username, Email = s.Email, Department = s.Department, IsActive = s.IsActive });
    }

    [HttpPost("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id, StudentFormVm vm)
    {
        var s = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Student);
        if (s == null) return NotFound();
        vm.Id = id;
        if (!ModelState.IsValid) return View("Form", vm);
        var username = vm.Username.Trim();
        var email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim();
        if (await db.Users.AnyAsync(u => u.Id != id && u.Username == username))
            ModelState.AddModelError(nameof(vm.Username), "A user with this ID already exists.");
        if (email != null && await db.Users.AnyAsync(u => u.Id != id && u.Email == email))
            ModelState.AddModelError(nameof(vm.Email), "A user with this email already exists.");
        if (!ModelState.IsValid) return View("Form", vm);
        s.FullName = vm.FullName.Trim(); s.Username = username; s.Email = email;
        s.Department = string.IsNullOrWhiteSpace(vm.Department) ? null : vm.Department.Trim();
        s.IsActive = vm.IsActive;
        await db.SaveChangesAsync();
        TempData["Success"] = "Student updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var s = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Student);
        if (s == null) return NotFound();
        var temp = Passwords.Generate();
        s.PasswordHash = Passwords.Hash(temp);
        s.MustChangePassword = true;
        s.FailedLogins = 0; s.LockoutEnd = null;
        await db.SaveChangesAsync();
        TempData["TempPassword"] = temp;
        TempData["TempFor"] = s.Username;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.Role == UserRole.Student);
        if (s == null) return NotFound();
        db.Users.Remove(s);
        await db.SaveChangesAsync();
        TempData["Success"] = $"Deleted {s.FullName} and their exam records.";
        return RedirectToAction(nameof(Index));
    }
}
