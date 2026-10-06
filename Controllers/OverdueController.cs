using Microsoft.AspNetCore.Authorization;
using System.Net;
using System.Net.Mail;
using FinanceDesk.Data;
using FinanceDesk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceDesk.Controllers;

public class OverdueController : Controller
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    public OverdueController(AppDbContext db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    private async Task<List<Invoice>> GetOverdue() =>
        (await _db.Invoices.Include(i => i.Student).Include(i => i.Fee).Include(i => i.Payments)
            .Where(i => !i.IsArchived && i.DueDate < DateTime.Today).ToListAsync())
        .Where(i => i.Balance > 0).OrderBy(i => i.DueDate).ToList();

    public async Task<IActionResult> Index()
    {
        ViewBag.Pct = _cfg.GetValue<decimal>("Billing:LateFeePercent");
        ViewBag.Logs = await _db.ReminderLogs.Include(l => l.Invoice).ThenInclude(i => i.Student)
            .OrderByDescending(l => l.SentAt).Take(10).ToListAsync();
        return View(await GetOverdue());
    }

    private decimal ApplyLateFee(Invoice inv)
    {
        if (inv.LateFee > 0) return 0;   // only once per invoice
        var pct = _cfg.GetValue<decimal>("Billing:LateFeePercent");
        inv.LateFee = Math.Round(inv.Balance * pct / 100, 2);
        return inv.LateFee;
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApplyLateFee(int id)
    {
        var inv = await _db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == id);
        if (inv == null) return NotFound();
        var fee = ApplyLateFee(inv);
        await _db.SaveChangesAsync();
        TempData[fee > 0 ? "Msg" : "Err"] = fee > 0 ? $"Late fee of {fee.Money()} applied." : "A late fee was already applied to this invoice.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApplyAllLateFees()
    {
        int n = 0;
        foreach (var inv in await GetOverdue()) if (ApplyLateFee(inv) > 0) n++;
        await _db.SaveChangesAsync();
        TempData["Msg"] = $"Late fees applied to {n} invoice(s).";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remind(int id, string channel)
    {
        var inv = await _db.Invoices.Include(i => i.Student).Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == id);
        if (inv == null) return NotFound();
        if (channel != "Email" && channel != "SMS") return BadRequest("Unknown reminder channel.");
        var result = await SendReminder(inv, channel);
        await _db.SaveChangesAsync();
        TempData["Msg"] = $"{channel} reminder: {result}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemindAll()
    {
        int n = 0;
        foreach (var inv in await GetOverdue()) { await SendReminder(inv, "Email"); n++; }
        await _db.SaveChangesAsync();
        TempData["Msg"] = $"Email reminders processed for {n} invoice(s).";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> SendReminder(Invoice inv, string channel)
    {
        var msg = $"Dear {inv.Student.FullName}, invoice {inv.InvoiceNo} with a balance of {inv.Balance.Money()} " +
                  $"was due on {inv.DueDate:MMM dd, yyyy}. Please settle your account. Thank you.";
        string result;
        if (channel == "Email")
        {
            var host = _cfg["Smtp:Host"];
            if (string.IsNullOrWhiteSpace(inv.Student.Email)) result = "Skipped - student has no email";
            else if (string.IsNullOrWhiteSpace(host)) result = "Logged only (SMTP not configured)";
            else
            {
                try
                {
                    using var client = new SmtpClient(host, _cfg.GetValue<int>("Smtp:Port"))
                    {
                        EnableSsl = _cfg.GetValue<bool>("Smtp:EnableSsl"),
                        Credentials = new NetworkCredential(_cfg["Smtp:User"], _cfg["Smtp:Password"])
                    };
                    await client.SendMailAsync(new MailMessage(_cfg["Smtp:From"], inv.Student.Email, "Payment Reminder - " + inv.InvoiceNo, msg));
                    result = "Sent";
                }
                catch (Exception ex) { result = "Failed: " + ex.Message; }
            }
        }
        else
        {
            // SMS: plug in a provider (Twilio, Semaphore, etc.) here. For now it is logged only.
            result = string.IsNullOrWhiteSpace(inv.Student.Phone) ? "Skipped - student has no phone" : "Logged only (SMS gateway not configured)";
        }
        _db.ReminderLogs.Add(new ReminderLog { InvoiceId = inv.Id, Channel = channel, Message = msg, Result = result });
        return result;
    }
}
