using FinanceDesk.Data;
using FinanceDesk.Models;
using FinanceDesk.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

var conn = builder.Configuration.GetConnectionString("DefaultConnection");
// Detect MySQL vs MariaDB (e.g. XAMPP) automatically; fall back to MySQL 8 if the server can't be reached yet.
ServerVersion serverVersion;
try
{
    var probe = new MySqlConnectionStringBuilder(conn) { Database = "" };   // the database may not exist yet
    serverVersion = ServerVersion.AutoDetect(probe.ConnectionString);
}
catch
{
    serverVersion = new MySqlServerVersion(new Version(8, 0, 36));
}
builder.Services.AddDbContext<AppDbContext>(o => o.UseMySql(conn, serverVersion));

// ---- Login (cookie authentication) ----
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Cookie.HttpOnly = true;
        o.Cookie.Name = "FinanceDesk.Auth";

        // Re-check the account on every request: a deactivated/deleted user (or one whose role
        // changed) must not keep working until the cookie expires.
        o.Events.OnValidatePrincipal = async ctx =>
        {
            var idText = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = ctx.Principal?.FindFirstValue(ClaimTypes.Role);
            var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var u = int.TryParse(idText, out var id)
                ? await db.Users.AsNoTracking().Where(x => x.Id == id)
                      .Select(x => new { x.IsActive, x.Role }).FirstOrDefaultAsync()
                : null;
            if (u == null || !u.IsActive || u.Role != role)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

// Every page requires login unless marked [AllowAnonymous]
builder.Services.AddAuthorization(o =>
    o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();

// Create database + tables on first run and seed starter data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Create the database + tables automatically; retry a few times in case MySQL is still starting.
    for (var attempt = 1; ; attempt++)
    {
        try { db.Database.EnsureCreated(); break; }
        catch (Exception ex)
        {
            if (attempt >= 5)
            {
                Console.Error.WriteLine("\nFinanceDesk could not connect to MySQL.");
                Console.Error.WriteLine("Make sure MySQL is running and that the User/Password in appsettings.json are correct.");
                Console.Error.WriteLine("Details: " + ex.Message);
                return;
            }
            Console.WriteLine($"Waiting for MySQL ({attempt}/5): {ex.Message}");
            Thread.Sleep(3000);
        }
    }

    if (!db.Users.Any())
    {
        db.Users.AddRange(
            new User { Username = "admin", FullName = "System Administrator", Role = "Admin", PasswordHash = PasswordHelper.Hash("Admin@123") },
            new User { Username = "cashier", FullName = "Cashier Account", Role = "Cashier", PasswordHash = PasswordHelper.Hash("Cashier@123") });
        db.SaveChanges();
    }
    if (!db.Fees.Any())
    {
        db.Fees.AddRange(
            new Fee { Name = "Tuition (per term)", Category = "Tuition", Amount = 25000, IsActive = true },
            new Fee { Name = "Laboratory Fee", Category = "Laboratory", Amount = 1500, IsActive = true },
            new Fee { Name = "Library Fee", Category = "Library", Amount = 500, IsActive = true },
            new Fee { Name = "Miscellaneous Fee", Category = "Miscellaneous", Amount = 2000, IsActive = true });
        db.Discounts.AddRange(
            new Discount { Name = "Academic Scholarship (50%)", Type = "Percentage", Value = 50, IsActive = true },
            new Discount { Name = "Sibling Discount (10%)", Type = "Percentage", Value = 10, IsActive = true },
            new Discount { Name = "Early Bird Promo (PHP 500)", Type = "Fixed", Value = 500, IsActive = true });
        db.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Home/Error");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
