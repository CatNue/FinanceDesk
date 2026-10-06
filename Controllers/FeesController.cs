using Microsoft.AspNetCore.Authorization;
using FinanceDesk.Data;
using FinanceDesk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceDesk.Controllers;

[Authorize(Roles = "Admin")]
public class FeesController : Controller
{
    private readonly AppDbContext _db;
    public FeesController(AppDbContext db) { _db = db; }

    public async Task<IActionResult> Index() => View(await _db.Fees.OrderBy(f => f.Category).ThenBy(f => f.Name).ToListAsync());

    public IActionResult Create() => View(new Fee());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Fee f)
    {
        if (!ModelState.IsValid) return View(f);
        _db.Add(f);
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Fee added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var f = await _db.Fees.FindAsync(id);
        return f == null ? NotFound() : View(f);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Fee f)
    {
        if (id != f.Id) return NotFound();
        if (!ModelState.IsValid) return View(f);
        _db.Update(f);
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Fee updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var f = await _db.Fees.FindAsync(id);
        if (f == null) return NotFound();
        if (await _db.Invoices.AnyAsync(i => i.FeeId == id))
        {
            TempData["Err"] = "This fee is used by invoices. Deactivate it instead of deleting.";
            return RedirectToAction(nameof(Index));
        }
        _db.Remove(f);
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Fee deleted.";
        return RedirectToAction(nameof(Index));
    }
}
