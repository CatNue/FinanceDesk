using Microsoft.AspNetCore.Authorization;
using FinanceDesk.Data;
using FinanceDesk.Models;
using FinanceDesk.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace FinanceDesk.Controllers;

public class InvoicesController : Controller
{
    private readonly AppDbContext _db;
    public InvoicesController(AppDbContext db) { _db = db; }

    private async Task LoadLists(int? feeId = null, int? discountId = null)
    {
        var students = await _db.Students.OrderBy(s => s.FullName).ToListAsync();
        ViewBag.Students = new SelectList(students.Select(s => new { s.Id, Name = $"{s.StudentNo} - {s.FullName}" }), "Id", "Name");
        ViewBag.Fees = new SelectList(await _db.Fees.Where(f => f.IsActive || f.Id == feeId).OrderBy(f => f.Name).ToListAsync(), "Id", "Name");
        ViewBag.Discounts = new SelectList(await _db.Discounts.Where(d => d.IsActive || d.Id == discountId).OrderBy(d => d.Name).ToListAsync(), "Id", "Name");
    }

    public async Task<IActionResult> Index(string q, string status, bool archived = false)
    {
        if (!User.IsInRole("Admin")) archived = false;
        var query = _db.Invoices.Include(i => i.Student).Include(i => i.Fee).Include(i => i.Payments)
            .Where(i => i.IsArchived == archived);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(i => i.InvoiceNo.Contains(q) || i.Student.FullName.Contains(q) || i.Student.StudentNo.Contains(q));
        var list = await query.OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.Id).ToListAsync();
        if (!string.IsNullOrEmpty(status)) list = list.Where(i => i.Status == status).ToList();
        ViewBag.Q = q; ViewBag.Status = status; ViewBag.Archived = archived;
        return View(list);
    }

    public async Task<IActionResult> Details(int id)
    {
        var inv = await _db.Invoices.Include(i => i.Student).Include(i => i.Fee).Include(i => i.Discount)
            .Include(i => i.Payments).ThenInclude(p => p.Receipt)
            .FirstOrDefaultAsync(i => i.Id == id);
        return inv == null ? NotFound() : View(inv);
    }

    public async Task<IActionResult> Create(int? studentId)
    {
        await LoadLists();
        return View(new Invoice { StudentId = studentId ?? 0 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("StudentId,FeeId,DiscountId,Amount,IssueDate,DueDate,Installments,Notes")] Invoice inv)
    {
        if (inv.DueDate < inv.IssueDate) ModelState.AddModelError("DueDate", "Due date cannot be before the issue date.");
        if (inv.StudentId == 0) ModelState.AddModelError("StudentId", "Select a student.");
        if (inv.FeeId == 0) ModelState.AddModelError("FeeId", "Select a fee.");
        var fee = inv.FeeId == 0 ? null : await _db.Fees.FindAsync(inv.FeeId);
        if (inv.FeeId != 0 && (fee == null || !fee.IsActive)) ModelState.AddModelError("FeeId", "Select an active fee.");
        if (inv.StudentId != 0 && !await _db.Students.AnyAsync(s => s.Id == inv.StudentId)) ModelState.AddModelError("StudentId", "Select a valid student.");
        if (!ModelState.IsValid) { await LoadLists(); return View(inv); }

        var disc = inv.DiscountId.HasValue ? await _db.Discounts.FindAsync(inv.DiscountId) : null;
        if (!User.IsInRole("Admin")) { inv.Amount = 0; inv.DiscountId = null; disc = null; }   // cashiers use standard fees only
        if (inv.Amount <= 0) inv.Amount = fee.Amount;
        inv.DiscountAmount = BillingHelper.DiscountFor(inv.Amount, disc);
        inv.InvoiceNo = BillingHelper.NewNo("INV");
        _db.Add(inv);
        await _db.SaveChangesAsync();
        TempData["Msg"] = $"Invoice {inv.InvoiceNo} created.";
        return RedirectToAction(nameof(Details), new { id = inv.Id });
    }

    // ---- Batch generation for a whole grade level (or all students) ----
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Batch()
    {
        await LoadBatchLists();
        return View(new BatchVM());
    }

    private async Task LoadBatchLists()
    {
        await LoadLists();
        var levels = await _db.Students.Select(s => s.GradeLevel).Distinct().OrderBy(x => x).ToListAsync();
        ViewBag.Levels = new SelectList(levels);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Batch(BatchVM vm)
    {
        if (vm.DueDate < vm.IssueDate) ModelState.AddModelError("DueDate", "Due date cannot be before the issue date.");
        var fee = await _db.Fees.FindAsync(vm.FeeId);
        if (fee == null || !fee.IsActive) ModelState.AddModelError("FeeId", "Select an active fee.");
        if (!ModelState.IsValid) { await LoadBatchLists(); return View(vm); }

        var disc = vm.DiscountId.HasValue ? await _db.Discounts.FindAsync(vm.DiscountId) : null;
        var students = _db.Students.AsQueryable();
        if (!string.IsNullOrEmpty(vm.GradeLevel)) students = students.Where(s => s.GradeLevel == vm.GradeLevel);

        int count = 0;
        foreach (var s in await students.ToListAsync())
        {
            _db.Invoices.Add(new Invoice
            {
                InvoiceNo = BillingHelper.NewNo("INV"),
                StudentId = s.Id, FeeId = fee.Id, DiscountId = vm.DiscountId,
                Amount = fee.Amount, DiscountAmount = BillingHelper.DiscountFor(fee.Amount, disc),
                IssueDate = vm.IssueDate, DueDate = vm.DueDate, Installments = vm.Installments
            });
            count++;
        }
        if (count == 0)
        {
            TempData["Err"] = "No students matched the selected grade level, so no invoices were generated.";
            return RedirectToAction(nameof(Batch));
        }
        await _db.SaveChangesAsync();
        TempData["Msg"] = $"{count} invoice(s) generated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]

    public async Task<IActionResult> Edit(int id)
    {
        var inv = await _db.Invoices.FindAsync(id);
        if (inv == null) return NotFound();
        await LoadLists(inv.FeeId, inv.DiscountId);
        return View(inv);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(int id, [Bind("Id,DiscountId,Amount,LateFee,IssueDate,DueDate,Installments,Notes")] Invoice form)
    {
        var inv = await _db.Invoices.FindAsync(id);
        if (inv == null || id != form.Id) return NotFound();
        if (form.DueDate < form.IssueDate) ModelState.AddModelError("DueDate", "Due date cannot be before the issue date.");
        if (form.Amount <= 0) ModelState.AddModelError("Amount", "Amount must be greater than zero.");
        if (!ModelState.IsValid)
        {
            await LoadLists(inv.FeeId, inv.DiscountId);
            form.StudentId = inv.StudentId; form.FeeId = inv.FeeId; form.InvoiceNo = inv.InvoiceNo;
            return View(form);
        }
        var disc = form.DiscountId.HasValue ? await _db.Discounts.FindAsync(form.DiscountId) : null;
        inv.DiscountId = form.DiscountId;
        inv.Amount = form.Amount;
        inv.DiscountAmount = BillingHelper.DiscountFor(form.Amount, disc);
        inv.LateFee = form.LateFee;
        inv.IssueDate = form.IssueDate;
        inv.DueDate = form.DueDate;
        inv.Installments = form.Installments;
        inv.Notes = form.Notes;
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Invoice updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Archive(int id)
    {
        var inv = await _db.Invoices.FindAsync(id);
        if (inv == null) return NotFound();
        inv.IsArchived = !inv.IsArchived;
        await _db.SaveChangesAsync();
        TempData["Msg"] = inv.IsArchived ? "Invoice archived." : "Invoice restored.";
        return RedirectToAction(nameof(Index), new { archived = !inv.IsArchived });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var inv = await _db.Invoices.FindAsync(id);
        if (inv == null) return NotFound();
        if (await _db.Payments.AnyAsync(p => p.InvoiceId == id))
        {
            TempData["Err"] = "This invoice has payments and cannot be deleted. Archive it instead.";
            return RedirectToAction(nameof(Details), new { id });
        }
        _db.Remove(inv);
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Invoice deleted.";
        return RedirectToAction(nameof(Index));
    }
}
