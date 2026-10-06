using System.Text;
using FinanceDesk.Data;
using FinanceDesk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceDesk.Controllers;

public class ReceiptsController : Controller
{
    private readonly AppDbContext _db;
    public ReceiptsController(AppDbContext db) { _db = db; }

    private IQueryable<Receipt> Full() => _db.Receipts
        .Include(r => r.Payment).ThenInclude(p => p.Invoice).ThenInclude(i => i.Student)
        .Include(r => r.Payment).ThenInclude(p => p.Invoice).ThenInclude(i => i.Fee)
        .Include(r => r.Payment).ThenInclude(p => p.Invoice).ThenInclude(i => i.Payments);

    // Receipt log (audit)
    public async Task<IActionResult> Index(string q)
    {
        var query = Full();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(r => r.ReceiptNo.Contains(q) || r.Payment.Invoice.Student.FullName.Contains(q));
        ViewBag.Q = q;
        return View(await query.OrderByDescending(r => r.IssuedAt).ToListAsync());
    }

    // print=true -> counts as a reprint (logged) and auto-opens the print dialog
    public async Task<IActionResult> Details(int id, bool print = false)
    {
        var r = await Full().FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound();
        if (print)
        {
            r.PrintCount++;
            r.LastPrintedAt = DateTime.Now;
            await _db.SaveChangesAsync();
        }
        ViewBag.AutoPrint = print;
        return View(r);
    }

    // Digital download (plain-text copy)
    public async Task<IActionResult> Download(int id)
    {
        var r = await Full().FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound();
        var p = r.Payment; var inv = p.Invoice;
        var sb = new StringBuilder();
        sb.AppendLine("=========== OFFICIAL RECEIPT ===========");
        sb.AppendLine($"Receipt No : {r.ReceiptNo}");
        sb.AppendLine($"Issued     : {r.IssuedAt:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"Student    : {inv.Student.StudentNo} - {inv.Student.FullName}");
        sb.AppendLine($"Invoice    : {inv.InvoiceNo} ({inv.Fee.Name})");
        sb.AppendLine($"Method     : {p.Method}  Ref: {p.ReferenceNo}");
        sb.AppendLine($"Amount Paid: {p.Amount.Money()}");
        sb.AppendLine($"Balance    : {inv.Balance.Money()}");
        sb.AppendLine("========================================");
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/plain", $"{r.ReceiptNo}.txt");
    }
}
