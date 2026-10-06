using System.Security.Claims;
using FinanceDesk.Data;
using FinanceDesk.Models;
using FinanceDesk.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceDesk.Controllers;

// ========================= 1. CREATE ACCOUNT (admin-managed, by design: no public sign-up for a finance system) =========================
[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) { _db = db; }

    private int MyId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

    public async Task<IActionResult> Index() =>
        View(await _db.Users.OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync());

    public IActionResult Create() => View(new UserCreateVM());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateVM vm)
    {
        if (vm.Role != "Admin" && vm.Role != "Cashier") ModelState.AddModelError("Role", "Invalid role.");
        if (await _db.Users.AnyAsync(u => u.Username == vm.Username))
            ModelState.AddModelError("Username", "Username is already taken.");
        if (!string.IsNullOrWhiteSpace(vm.Email) && await _db.Users.AnyAsync(u => u.Email == vm.Email))
            ModelState.AddModelError("Email", "That email address is already used by another account.");
        if (!ModelState.IsValid) return View(vm);

        _db.Users.Add(new User { Username = vm.Username, FullName = vm.FullName, Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim(), Role = vm.Role, PasswordHash = PasswordHelper.Hash(vm.Password) });
        await _db.SaveChangesAsync();
        TempData["Msg"] = $"Account '{vm.Username}' created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var u = await _db.Users.FindAsync(id);
        if (u == null) return NotFound();
        return View(new UserEditVM { Id = u.Id, Username = u.Username, FullName = u.FullName, Email = u.Email, Role = u.Role, IsActive = u.IsActive });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditVM vm)
    {
        var u = await _db.Users.FindAsync(vm.Id);
        if (u == null) return NotFound();
        vm.Username = u.Username;
        if (vm.Role != "Admin" && vm.Role != "Cashier") ModelState.AddModelError("Role", "Invalid role.");

        bool losingAdmin = u.Role == "Admin" && u.IsActive && (vm.Role != "Admin" || !vm.IsActive);
        if (losingAdmin)
        {
            if (u.Id == MyId) ModelState.AddModelError("", "You cannot remove your own admin access or deactivate yourself.");
            else if (await _db.Users.CountAsync(x => x.Role == "Admin" && x.IsActive) <= 1)
                ModelState.AddModelError("", "There must be at least one active administrator.");
        }
        if (!string.IsNullOrWhiteSpace(vm.Email) && await _db.Users.AnyAsync(x => x.Email == vm.Email && x.Id != vm.Id))
            ModelState.AddModelError("Email", "That email address is already used by another account.");
        if (!ModelState.IsValid) return View(vm);

        u.FullName = vm.FullName; u.Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim(); u.Role = vm.Role; u.IsActive = vm.IsActive;
        await _db.SaveChangesAsync();
        TempData["Msg"] = "User updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> ResetPassword(int id)
    {
        var u = await _db.Users.FindAsync(id);
        return u == null ? NotFound() : View(new ResetPasswordVM { Id = u.Id, Username = u.Username });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordVM vm)
    {
        var u = await _db.Users.FindAsync(vm.Id);
        if (u == null) return NotFound();
        vm.Username = u.Username;
        if (!ModelState.IsValid) return View(vm);
        u.PasswordHash = PasswordHelper.Hash(vm.NewPassword);
        u.FailedAttempts = 0; u.LockoutEnd = null;
        await _db.SaveChangesAsync();
        TempData["Msg"] = $"Password reset for '{u.Username}'.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var u = await _db.Users.FindAsync(id);
        if (u == null) return NotFound();
        if (u.IsActive && u.Role == "Admin")
        {
            if (u.Id == MyId) { TempData["Err"] = "You cannot deactivate your own account."; return RedirectToAction(nameof(Index)); }
            if (await _db.Users.CountAsync(x => x.Role == "Admin" && x.IsActive) <= 1)
            { TempData["Err"] = "There must be at least one active administrator."; return RedirectToAction(nameof(Index)); }
        }
        u.IsActive = !u.IsActive;
        await _db.SaveChangesAsync();
        TempData["Msg"] = u.IsActive ? "Account activated." : "Account deactivated.";
        return RedirectToAction(nameof(Index));
    }

    // ========================= 7. DELETE ACCOUNT (admin managing another account) =========================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var u = await _db.Users.FindAsync(id);
        if (u == null) return NotFound();
        if (u.Id == MyId) { TempData["Err"] = "You cannot delete your own account here. Use \"Delete my account\" in your profile instead."; return RedirectToAction(nameof(Index)); }
        if (u.Role == "Admin" && await _db.Users.CountAsync(x => x.Role == "Admin" && x.IsActive) <= 1 && u.IsActive)
        { TempData["Err"] = "There must be at least one active administrator."; return RedirectToAction(nameof(Index)); }

        _db.Users.Remove(u);
        await _db.SaveChangesAsync();
        TempData["Msg"] = $"Account '{u.Username}' deleted.";
        return RedirectToAction(nameof(Index));
    }
}
