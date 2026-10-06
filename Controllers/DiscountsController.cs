using Microsoft.AspNetCore.Authorization;
using FinanceDesk.Data;
using FinanceDesk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceDesk.Controllers;

[Authorize(Roles = "Admin")]
public class DiscountsController : Controller
{
    private readonly AppDbContext _db;
    public DiscountsController(AppDbContext db) { _db = db; }

    public async Task<IActionResult> Index() => View(await _db.Discounts.OrderBy(d => d.Name).ToListAsync());

    public IActionResult Create() => View(new Discount());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Discount d)
    {
        if (d.Type != "Percentage" && d.Type != "Fixed") ModelState.AddModelError("Type", "Choose Percentage or Fixed.");
        if (d.Type == "Percentage" && d.Value > 100) ModelState.AddModelError("Value", "Percentage cannot exceed 100.");
        if (!ModelState.IsValid) return View(d);
        _db.Add(d);
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Discount added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var d = await _db.Discounts.FindAsync(id);
        return d == null ? NotFound() : View(d);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Discount d)
    {
        if (id != d.Id) return NotFound();
        if (d.Type != "Percentage" && d.Type != "Fixed") ModelState.AddModelError("Type", "Choose Percentage or Fixed.");
        if (d.Type == "Percentage" && d.Value > 100) ModelState.AddModelError("Value", "Percentage cannot exceed 100.");
        if (!ModelState.IsValid) return View(d);
        _db.Update(d);
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Discount updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var d = await _db.Discounts.FindAsync(id);
        if (d == null) return NotFound();
        _db.Remove(d);   // invoices keep their computed discount amount (FK set to null)
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Discount deleted.";
        return RedirectToAction(nameof(Index));
    }
}
