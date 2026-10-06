using FinanceDesk.Data;
using FinanceDesk.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceDesk.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) { _db = db; }

    // Admins and cashiers each get their own dashboard
    public async Task<IActionResult> Index() =>
        User.IsInRole("Admin") ? await AdminDashboard() : await CashierDashboard();

    // ======================= ADMIN =======================
    private async Task<IActionResult> AdminDashboard()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var trendStart = monthStart.AddMonths(-5);

        var invoices = await _db.Invoices.Include(i => i.Payments).Include(i => i.Student)
            .Where(i => !i.IsArchived).ToListAsync();
        var open = invoices.Where(i => i.Balance > 0).ToList();
        var pays = await _db.Payments.Include(p => p.Invoice).ThenInclude(i => i.Fee)
            .Where(p => p.PaymentDate >= trendStart && p.PaymentDate < today.AddDays(1)).ToListAsync();

        decimal totalBilled = invoices.Sum(i => i.Total), totalPaid = invoices.Sum(i => i.AmountPaid);
        ViewBag.Outstanding = open.Sum(i => i.Balance);
        ViewBag.OpenCount = open.Count;
        ViewBag.OverdueTotal = open.Where(i => i.Status == "Overdue").Sum(i => i.Balance);
        ViewBag.OverdueCount = open.Count(i => i.Status == "Overdue");
        ViewBag.Month = pays.Where(p => p.PaymentDate >= monthStart).Sum(p => p.Amount);
        ViewBag.Today = pays.Where(p => p.PaymentDate >= today).Sum(p => p.Amount);
        ViewBag.Rate = totalBilled > 0 ? Math.Round(totalPaid / totalBilled * 100, 1) : 0m;
        ViewBag.Flagged = await _db.Payments.CountAsync(p => p.Status == "Flagged");
        ViewBag.Students = await _db.Students.CountAsync();
        ViewBag.ActiveUsers = await _db.Users.CountAsync(u => u.IsActive);

        var months = Enumerable.Range(0, 6).Select(i => trendStart.AddMonths(i)).ToList();
        ViewBag.MonthLabels = months.Select(m => m.ToString("MMM yyyy")).ToList();
        ViewBag.Billed = months.Select(m => invoices.Where(i => i.IssueDate.Year == m.Year && i.IssueDate.Month == m.Month).Sum(i => i.Total)).ToList();
        ViewBag.Collected = months.Select(m => pays.Where(p => p.PaymentDate.Year == m.Year && p.PaymentDate.Month == m.Month).Sum(p => p.Amount)).ToList();

        var cat = pays.Where(p => p.PaymentDate >= monthStart).GroupBy(p => p.Invoice.Fee.Category).ToList();
        ViewBag.CatLabels = cat.Select(g => g.Key).ToList();
        ViewBag.CatValues = cat.Select(g => g.Sum(p => p.Amount)).ToList();

        var cash = pays.Where(p => p.PaymentDate >= monthStart).GroupBy(p => string.IsNullOrEmpty(p.ReceivedBy) ? "Unassigned" : p.ReceivedBy).ToList();
        ViewBag.CashierLabels = cash.Select(g => g.Key).ToList();
        ViewBag.CashierValues = cash.Select(g => g.Sum(p => p.Amount)).ToList();

        ViewBag.StatusCounts = new List<int>
        {
            invoices.Count(i => i.Status == "Paid"), invoices.Count(i => i.Status == "Partial"),
            invoices.Count(i => i.Status == "Unpaid"), invoices.Count(i => i.Status == "Overdue")
        };
        ViewBag.TopOverdue = open.Where(i => i.Status == "Overdue").OrderByDescending(i => i.Balance).Take(6).ToList();
        ViewBag.Activity = await _db.AuditLogs.OrderByDescending(a => a.Timestamp).Take(8).ToListAsync();
        return View("AdminDashboard");
    }

    // ======================= CASHIER =======================
    private async Task<IActionResult> CashierDashboard()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var me = User.FindFirst("FullName")?.Value ?? User.Identity?.Name;

        var mine = await _db.Payments.Include(p => p.Invoice).ThenInclude(i => i.Student)
            .Where(p => p.ReceivedBy == me && p.PaymentDate >= monthStart && p.PaymentDate < today.AddDays(1)).ToListAsync();
        var myToday = mine.Where(p => p.PaymentDate >= today).ToList();

        ViewBag.Me = me;
        ViewBag.MyToday = myToday.Sum(p => p.Amount);
        ViewBag.MyTodayCount = myToday.Count;
        ViewBag.MyMonth = mine.Sum(p => p.Amount);
        ViewBag.AllToday = await _db.Payments.Where(p => p.PaymentDate >= today && p.PaymentDate < today.AddDays(1)).SumAsync(p => p.Amount);
        ViewBag.ByMethod = myToday.GroupBy(p => p.Method).Select(g => new KeyValuePair<string, decimal>(g.Key, g.Sum(p => p.Amount))).OrderByDescending(x => x.Value).ToList();
        ViewBag.MyRecent = mine.OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id).Take(8).ToList();

        var open = (await _db.Invoices.Include(i => i.Student).Include(i => i.Payments)
            .Where(i => !i.IsArchived).ToListAsync()).Where(i => i.Balance > 0).ToList();
        ViewBag.OverdueCount = open.Count(i => i.Status == "Overdue");
        ViewBag.DueSoon = open.Where(i => i.DueDate.Date >= today && i.DueDate.Date <= today.AddDays(7))
            .OrderBy(i => i.DueDate).Take(8).ToList();
        return View("CashierDashboard");
    }

    [AllowAnonymous]
    public IActionResult Error() => Content("Something went wrong.");
}
