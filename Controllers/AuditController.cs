using FinanceDesk.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceDesk.Controllers;

[Authorize(Roles = "Admin")]
public class AuditController : Controller
{
    private readonly AppDbContext _db;
    public AuditController(AppDbContext db) { _db = db; }

    public async Task<IActionResult> Index(string q, string user, string action, DateTime? from, DateTime? to)
    {
        var query = _db.AuditLogs.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(a => a.Details.Contains(q) || a.Entity.Contains(q));
        if (!string.IsNullOrWhiteSpace(user)) query = query.Where(a => a.Username == user);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);
        if (from.HasValue) query = query.Where(a => a.Timestamp >= from.Value.Date);
        if (to.HasValue) query = query.Where(a => a.Timestamp < to.Value.Date.AddDays(1));

        ViewBag.Users = await _db.Users.Select(u => u.Username).OrderBy(x => x).ToListAsync();
        ViewBag.Q = q; ViewBag.User = user; ViewBag.Action = action; ViewBag.From = from; ViewBag.To = to;
        return View(await query.OrderByDescending(a => a.Timestamp).Take(300).ToListAsync());
    }
}
