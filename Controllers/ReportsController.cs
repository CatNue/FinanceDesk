using Microsoft.AspNetCore.Authorization;
using ClosedXML.Excel;
using FinanceDesk.Data;
using FinanceDesk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanceDesk.Controllers;

[Authorize(Roles = "Admin")]
public class ReportsController : Controller
{
    private readonly AppDbContext _db;
    public ReportsController(AppDbContext db) { _db = db; }

    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private Task<List<Payment>> PaymentsBetween(DateTime from, DateTime to) =>
        _db.Payments.Include(p => p.Invoice).ThenInclude(i => i.Student)
            .Where(p => p.PaymentDate >= from.Date && p.PaymentDate < to.Date.AddDays(1))
            .OrderBy(p => p.PaymentDate).ToListAsync();

    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var f = from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var t = to ?? DateTime.Today;
        if (t < f) (f, t) = (t, f);
        ViewBag.From = f; ViewBag.To = t;
        var payments = await PaymentsBetween(f, t);
        var invoices = await _db.Invoices.Include(i => i.Student).Include(i => i.Payments).Where(i => !i.IsArchived).ToListAsync();
        ViewBag.Outstanding = invoices.Where(i => i.Balance > 0).Sum(i => i.Balance);
        ViewBag.Billed = invoices.Where(i => i.IssueDate >= f && i.IssueDate <= t).Sum(i => i.Total);
        return View(payments);
    }

    public async Task<IActionResult> ExportPayments(DateTime? from, DateTime? to)
    {
        var f = from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var t = to ?? DateTime.Today;
        if (t < f) (f, t) = (t, f);
        var data = await PaymentsBetween(f, t);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Collections");
        string[] h = { "Date", "Student No.", "Student", "Invoice", "Method", "Reference", "Amount", "Status" };
        for (int c = 0; c < h.Length; c++) ws.Cell(1, c + 1).Value = h[c];
        ws.Row(1).Style.Font.Bold = true;
        int r = 2;
        foreach (var p in data)
        {
            ws.Cell(r, 1).Value = p.PaymentDate;
            ws.Cell(r, 2).Value = p.Invoice.Student.StudentNo;
            ws.Cell(r, 3).Value = p.Invoice.Student.FullName;
            ws.Cell(r, 4).Value = p.Invoice.InvoiceNo;
            ws.Cell(r, 5).Value = p.Method;
            ws.Cell(r, 6).Value = p.ReferenceNo;
            ws.Cell(r, 7).Value = p.Amount;
            ws.Cell(r, 8).Value = p.Status;
            r++;
        }
        ws.Cell(r, 6).Value = "TOTAL"; ws.Cell(r, 7).Value = data.Sum(p => p.Amount);
        ws.Row(r).Style.Font.Bold = true;
        ws.Column(1).Style.DateFormat.Format = "yyyy-mm-dd";
        ws.Column(7).Style.NumberFormat.Format = "#,##0.00";
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), XlsxType, $"collections_{f:yyyyMMdd}_{t:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> ExportOutstanding()
    {
        var data = (await _db.Invoices.Include(i => i.Student).Include(i => i.Fee).Include(i => i.Payments)
            .Where(i => !i.IsArchived).ToListAsync()).Where(i => i.Balance > 0).OrderBy(i => i.DueDate).ToList();
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Outstanding");
        string[] h = { "Invoice", "Student No.", "Student", "Fee", "Due Date", "Total", "Paid", "Balance", "Status" };
        for (int c = 0; c < h.Length; c++) ws.Cell(1, c + 1).Value = h[c];
        ws.Row(1).Style.Font.Bold = true;
        int r = 2;
        foreach (var i in data)
        {
            ws.Cell(r, 1).Value = i.InvoiceNo;
            ws.Cell(r, 2).Value = i.Student.StudentNo;
            ws.Cell(r, 3).Value = i.Student.FullName;
            ws.Cell(r, 4).Value = i.Fee.Name;
            ws.Cell(r, 5).Value = i.DueDate;
            ws.Cell(r, 6).Value = i.Total;
            ws.Cell(r, 7).Value = i.AmountPaid;
            ws.Cell(r, 8).Value = i.Balance;
            ws.Cell(r, 9).Value = i.Status;
            r++;
        }
        ws.Cell(r, 7).Value = "TOTAL"; ws.Cell(r, 8).Value = data.Sum(i => i.Balance);
        ws.Row(r).Style.Font.Bold = true;
        ws.Column(5).Style.DateFormat.Format = "yyyy-mm-dd";
        ws.Range(2, 6, r, 8).Style.NumberFormat.Format = "#,##0.00";
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), XlsxType, $"outstanding_{DateTime.Today:yyyyMMdd}.xlsx");
    }
}
