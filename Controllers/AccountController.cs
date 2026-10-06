using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using FinanceDesk.Data;
using FinanceDesk.Models;
using FinanceDesk.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceDesk.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly IWebHostEnvironment _env;
    public AccountController(AppDbContext db, IConfiguration cfg, IWebHostEnvironment env) { _db = db; _cfg = cfg; _env = env; }

    private static string Cut(string s, int max) => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];

    // ========================= 2. LOGIN =========================
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new LoginVM { ReturnUrl = returnUrl });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVM vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == vm.Username.Trim());

        if (user != null && user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.Now)
        {
            ModelState.AddModelError("", $"Too many failed attempts. Try again after {user.LockoutEnd:h:mm tt}.");
            return View(vm);
        }

        if (user == null || !PasswordHelper.Verify(vm.Password, user.PasswordHash))
        {
            if (user != null)
            {
                user.FailedAttempts++;
                if (user.FailedAttempts >= 5) { user.LockoutEnd = DateTime.Now.AddMinutes(5); user.FailedAttempts = 0; }
            }
            _db.AuditLogs.Add(new AuditLog { Username = Cut(vm.Username, 60), Action = "LoginFailed", Entity = "Account", Details = "Invalid credentials" });
            await _db.SaveChangesAsync();
            ModelState.AddModelError("", "Invalid username or password.");
            return View(vm);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError("", "This account has been deactivated. Please contact the administrator.");
            return View(vm);
        }

        user.FailedAttempts = 0; user.LockoutEnd = null; user.LastLogin = DateTime.Now;
        _db.AuditLogs.Add(new AuditLog { Username = user.Username, Action = "Login", Entity = "Account", Details = $"{user.FullName} signed in ({user.Role})" });
        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("FullName", user.FullName),
            new(ClaimTypes.Role, user.Role)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        // ---- 4. REMEMBER ME: persistent cookie that survives closing the browser ----
        var props = new AuthenticationProperties { IsPersistent = vm.RememberMe };
        if (vm.RememberMe) props.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);

        if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl)) return Redirect(vm.ReturnUrl);
        return RedirectToAction("Index", "Home");
    }

    // ========================= 5. LOGOUT =========================
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        _db.AuditLogs.Add(new AuditLog { Username = User.Identity?.Name, Action = "Logout", Entity = "Account", Details = "Signed out" });
        await _db.SaveChangesAsync();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    // ========================= 3. FORGOT PASSWORD =========================
    [AllowAnonymous, HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordVM());

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordVM vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var key = vm.UsernameOrEmail.Trim();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == key || u.Email == key);

        // Always show the same confirmation screen, whether or not the account exists,
        // so the form can't be used to find out which usernames/emails are registered.
        if (user == null || !user.IsActive) return View("ForgotPasswordSent");

        user.ResetToken = Guid.NewGuid().ToString("N");
        user.ResetTokenExpiry = DateTime.Now.AddMinutes(30);
        _db.AuditLogs.Add(new AuditLog { Username = user.Username, Action = "Updated", Entity = "Account", Details = "Password reset requested" });
        await _db.SaveChangesAsync();

        var link = Url.Action(nameof(ResetPassword), "Account", new { token = user.ResetToken }, Request.Scheme);
        var sent = false;
        if (!string.IsNullOrWhiteSpace(_cfg["Smtp:Host"]) && !string.IsNullOrWhiteSpace(user.Email))
        {
            try
            {
                using var client = new SmtpClient(_cfg["Smtp:Host"], _cfg.GetValue<int>("Smtp:Port"))
                {
                    EnableSsl = _cfg.GetValue<bool>("Smtp:EnableSsl"),
                    Credentials = new NetworkCredential(_cfg["Smtp:User"], _cfg["Smtp:Password"])
                };
                await client.SendMailAsync(new MailMessage(_cfg["Smtp:From"], user.Email, "FinanceDesk - Password Reset",
                    $"Hello {user.FullName},\n\nUse the link below to reset your FinanceDesk password. It expires in 30 minutes.\n\n{link}\n\nIf you did not request this, you can ignore this email."));
                sent = true;
            }
            catch { /* fall back to on-screen link below */ }
        }

        ViewBag.Sent = sent;
        // The link is only shown on screen in Development. In production it would let anyone take over
        // any account just by typing its username, so there it is delivered by email only.
        ViewBag.DevLink = (sent || !_env.IsDevelopment()) ? null : link;
        return View("ForgotPasswordSent");
    }

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> ResetPassword(string token)
    {
        var valid = !string.IsNullOrEmpty(token) &&
            await _db.Users.AnyAsync(u => u.ResetToken == token && u.ResetTokenExpiry > DateTime.Now);
        if (!valid) return View("ResetLinkInvalid");
        return View(new ResetPasswordPublicVM { Token = token });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordPublicVM vm)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.ResetToken == vm.Token && u.ResetTokenExpiry > DateTime.Now);
        if (user == null) return View("ResetLinkInvalid");
        if (!ModelState.IsValid) return View(vm);

        user.PasswordHash = PasswordHelper.Hash(vm.NewPassword);
        user.ResetToken = null; user.ResetTokenExpiry = null;
        user.FailedAttempts = 0; user.LockoutEnd = null;
        _db.AuditLogs.Add(new AuditLog { Username = user.Username, Action = "Updated", Entity = "Account", Details = "Password reset via forgot-password link" });
        await _db.SaveChangesAsync();

        TempData["Msg"] = "Your password has been reset. Please sign in.";
        return RedirectToAction(nameof(Login));
    }

    // ---- change password while signed in ----
    public IActionResult ChangePassword() => View(new ChangePasswordVM());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordVM vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var user = await _db.Users.FindAsync(id);
        if (user == null || !PasswordHelper.Verify(vm.CurrentPassword, user.PasswordHash))
        {
            ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
            return View(vm);
        }
        user.PasswordHash = PasswordHelper.Hash(vm.NewPassword);
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Your password has been changed.";
        return RedirectToAction("Index", "Home");
    }

    // ========================= 6. UPDATE ACCOUNT (my profile) =========================
    public async Task<IActionResult> Profile()
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var u = await _db.Users.FindAsync(id);
        if (u == null) return NotFound();
        return View(new ProfileVM { Username = u.Username, Role = u.Role, FullName = u.FullName, Email = u.Email });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileVM vm)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var u = await _db.Users.FindAsync(id);
        if (u == null) return NotFound();
        vm.Username = u.Username; vm.Role = u.Role;
        if (!string.IsNullOrWhiteSpace(vm.Email) && await _db.Users.AnyAsync(x => x.Email == vm.Email && x.Id != id))
            ModelState.AddModelError("Email", "That email address is already used by another account.");
        if (!ModelState.IsValid) return View(vm);

        u.FullName = vm.FullName; u.Email = string.IsNullOrWhiteSpace(vm.Email) ? null : vm.Email.Trim();
        await _db.SaveChangesAsync();

        // refresh the display name shown in the top bar without forcing re-login
        var identity = (ClaimsIdentity)User.Identity;
        identity.RemoveClaim(identity.FindFirst("FullName"));
        identity.AddClaim(new Claim("FullName", u.FullName));
        var current = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), current?.Properties);

        TempData["Msg"] = "Your profile has been updated.";
        return RedirectToAction(nameof(Profile));
    }

    // ========================= 7. DELETE ACCOUNT (self-service) =========================
    public IActionResult DeleteAccount() => View(new DeleteAccountVM());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(DeleteAccountVM vm)
    {
        var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
        var u = await _db.Users.FindAsync(id);
        if (u == null) return NotFound();
        if (!PasswordHelper.Verify(vm.Password, u.PasswordHash))
        {
            ModelState.AddModelError("Password", "Password is incorrect.");
            return View(vm);
        }
        if (u.Role == "Admin" && await _db.Users.CountAsync(x => x.Role == "Admin" && x.IsActive) <= 1)
        {
            ModelState.AddModelError("", "You are the only active administrator, so this account cannot be deleted. Promote another account to Admin first.");
            return View(vm);
        }

        _db.AuditLogs.Add(new AuditLog { Username = u.Username, Action = "Deleted", Entity = "Account", Details = $"{u.FullName} deleted their own account" });
        _db.Users.Remove(u);
        await _db.SaveChangesAsync();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Msg"] = "Your account has been deleted.";
        return RedirectToAction(nameof(Login));
    }
}
