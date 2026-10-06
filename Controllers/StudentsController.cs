using Microsoft.AspNetCore.Authorization;
using FinanceDesk.Data;
using FinanceDesk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceDesk.Controllers;

public class StudentsController : Controller
{
    private readonly AppDbContext _db;
    public StudentsController(AppDbContext db) { _db = db; }

    public async Task<IActionResult> Index(string q)
    {
        var query = _db.Students.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(s => s.FullName.Contains(q) || s.StudentNo.Contains(q) || s.GradeLevel.Contains(q));
        ViewBag.Q = q;
        return View(await query.OrderBy(s => s.FullName).ToListAsync());
    }

    // Student invoice history + balances
    public async Task<IActionResult> Details(int id)
    {
        var s = await _db.Students
            .Include(x => x.Invoices).ThenInclude(i => i.Fee)
            .Include(x => x.Invoices).ThenInclude(i => i.Payments)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        return View(s);
    }

    public IActionResult Create() => View(new Student());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("StudentNo,FullName,GradeLevel,Email,Phone")] Student s)
    {
        s.StudentNo = s.StudentNo?.Trim();
        if (await _db.Students.AnyAsync(x => x.StudentNo == s.StudentNo))
            ModelState.AddModelError("StudentNo", "Student number already exists.");
        if (!ModelState.IsValid) return View(s);
        _db.Add(s);
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Student added.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var s = await _db.Students.FindAsync(id);
        return s == null ? NotFound() : View(s);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,StudentNo,FullName,GradeLevel,Email,Phone")] Student s)
    {
        if (id != s.Id) return NotFound();
        s.StudentNo = s.StudentNo?.Trim();
        if (await _db.Students.AnyAsync(x => x.StudentNo == s.StudentNo && x.Id != id))
            ModelState.AddModelError("StudentNo", "Student number already exists.");
        if (!ModelState.IsValid) return View(s);
        _db.Update(s);
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Student updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var s = await _db.Students.FindAsync(id);
        if (s == null) return NotFound();
        if (await _db.Invoices.AnyAsync(i => i.StudentId == id))
        {
            TempData["Err"] = "Cannot delete a student who has invoices.";
            return RedirectToAction(nameof(Index));
        }
        _db.Remove(s);
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Student deleted.";
        return RedirectToAction(nameof(Index));
    }
}
