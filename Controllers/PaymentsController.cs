using Microsoft.AspNetCore.Authorization;
using FinanceDesk.Data;
using FinanceDesk.Models;
using FinanceDesk.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FinanceDesk.Controllers;

public class PaymentsController : Controller
{
    private readonly AppDbContext _db;
    public PaymentsController(AppDbContext db) { _db = db; }

    private async Task LoadLists()
    {
        var students = await _db.Students.OrderBy(s => s.FullName).ToListAsync();
        ViewBag.Students = new SelectList(students.Select(s => new { s.Id, Name = $"{s.StudentNo} - {s.FullName}" }), "Id", "Name");
        var open = (await _db.Invoices.Include(i => i.Student).Include(i => i.Payments)
            .Where(i => !i.IsArchived).ToListAsync()).Where(i => i.Balance > 0).OrderBy(i => i.DueDate).ToList();
        ViewBag.Invoices = new SelectList(open.Select(i => new
        {
            i.Id, Name = $"{i.InvoiceNo} | {i.Student.FullName} | Balance {i.Balance.Money()}"
        }), "Id", "Name");
        ViewBag.Methods = new SelectList(new[] { "Cash", "Bank Transfer", "GCash", "Credit Card", "Check" });
    }

    public async Task<IActionResult> Index(string q, bool flagged = false)
    {
        var query = _db.Payments.Include(p => p.Receipt).Include(p => p.Invoice).ThenInclude(i => i.Student).AsQueryable();
        if (flagged) query = query.Where(p => p.Status == "Flagged");
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.ReferenceNo.Contains(q) || p.Invoice.InvoiceNo.Contains(q) || p.Invoice.Student.FullName.Contains(q));
        ViewBag.Q = q; ViewBag.Flagged = flagged;
        return View(await query.OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id).ToListAsync());
    }

    public async Task<IActionResult> Create(int? invoiceId)
    {
        await LoadLists();
        var vm = new PaymentVM();
        if (invoiceId.HasValue)
        {
            var inv = await _db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == invoiceId);
            if (inv != null) { vm.StudentId = inv.StudentId; vm.InvoiceId = inv.Id; vm.Amount = Math.Max(inv.Balance, 0); }
        }
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PaymentVM vm)
    {
        List<Invoice> targets = new();
        if (ModelState.IsValid)
        {
            if (vm.InvoiceId.HasValue)
            {
                var inv = await _db.Invoices.Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == vm.InvoiceId);
                if (inv == null || inv.StudentId != vm.StudentId)
                    ModelState.AddModelError("InvoiceId", "Invoice does not belong to the selected student.");
                else targets.Add(inv);
            }
            else
            {
                // Automatic matching: oldest unpaid invoice first
                targets = (await _db.Invoices.Include(i => i.Payments)
                    .Where(i => i.StudentId == vm.StudentId && !i.IsArchived).ToListAsync())
                    .Where(i => i.Balance > 0).OrderBy(i => i.DueDate).ToList();
                if (!targets.Any()) ModelState.AddModelError("StudentId", "This student has no unpaid invoices.");
            }
        }
        if (!ModelState.IsValid) { await LoadLists(); return View(vm); }

        bool duplicateRef = !string.IsNullOrWhiteSpace(vm.ReferenceNo) &&
                            await _db.Payments.AnyAsync(p => p.ReferenceNo == vm.ReferenceNo);

        decimal remaining = vm.Amount;
        var created = new List<Payment>();
        for (int i = 0; i < targets.Count && remaining > 0; i++)
        {
            var t = targets[i];
            bool last = i == targets.Count - 1;
            // explicit invoice -> whole amount; auto-match -> fill oldest first, leftover goes to the last invoice
            decimal apply = vm.InvoiceId.HasValue ? vm.Amount : (last ? remaining : Math.Min(remaining, t.Balance));
            if (apply <= 0) break;

            var reasons = new List<string>();
            if (apply > t.Balance) reasons.Add($"Overpayment of {(apply - t.Balance).Money()}");
            if (duplicateRef) reasons.Add("Duplicate reference number");

            var p = new Payment
            {
                InvoiceId = t.Id, Amount = apply, PaymentDate = vm.PaymentDate, Method = vm.Method,
                ReferenceNo = vm.ReferenceNo,
                Status = reasons.Any() ? "Flagged" : "Reconciled",
                ReceivedBy = User.FindFirst("FullName")?.Value ?? User.Identity?.Name,
                Remarks = string.Join("; ", new[] { vm.Remarks }.Concat(reasons).Where(x => !string.IsNullOrWhiteSpace(x))),
                Receipt = new Receipt { ReceiptNo = BillingHelper.NewNo("OR"), IssuedAt = DateTime.Now }
            };
            _db.Payments.Add(p);
            created.Add(p);
            remaining -= apply;
        }
        await _db.SaveChangesAsync();

        TempData["Msg"] = $"{created.Count} payment(s) recorded and receipt(s) generated." +
                          (created.Any(c => c.Status == "Flagged") ? " Some were flagged for review." : "");
        return created.Count == 1
            ? RedirectToAction("Details", "Receipts", new { id = created[0].Receipt.Id })
            : RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]

    public async Task<IActionResult> Edit(int id)
    {
        var p = await _db.Payments.Include(x => x.Invoice).ThenInclude(i => i.Student).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        ViewBag.Methods = new SelectList(new[] { "Cash", "Bank Transfer", "GCash", "Credit Card", "Check" }, p.Method);
        return View(p);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Amount,PaymentDate,Method,ReferenceNo,Remarks")] Payment form)
    {
        var p = await _db.Payments.Include(x => x.Invoice).ThenInclude(i => i.Payments).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null || id != form.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewBag.Methods = new SelectList(new[] { "Cash", "Bank Transfer", "GCash", "Credit Card", "Check" }, form.Method);
            form.Invoice = p.Invoice;
            return View(form);
        }
        // Discrepancy check against what the invoice balance would be without this payment
        decimal balanceWithoutThis = p.Invoice.Balance + p.Amount;
        p.Amount = form.Amount; p.PaymentDate = form.PaymentDate; p.Method = form.Method;
        p.ReferenceNo = form.ReferenceNo; p.Remarks = form.Remarks;
        p.Status = form.Amount > balanceWithoutThis ? "Flagged" : "Reconciled";
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Payment updated.";
        return RedirectToAction(nameof(Index));
    }

    // Reviewer confirms a flagged payment is OK
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Resolve(int id)
    {
        var p = await _db.Payments.FindAsync(id);
        if (p == null) return NotFound();
        p.Status = "Reconciled";
        p.Remarks = (p.Remarks + " [Reviewed " + DateTime.Now.ToString("yyyy-MM-dd") + "]").Trim();
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Payment marked as reconciled.";
        return RedirectToAction(nameof(Index), new { flagged = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.Payments.FindAsync(id);
        if (p == null) return NotFound();
        _db.Remove(p);   // its receipt is removed by cascade
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Payment deleted.";
        return RedirectToAction(nameof(Index));
    }
}
