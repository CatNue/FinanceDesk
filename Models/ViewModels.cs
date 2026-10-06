using System.ComponentModel.DataAnnotations;

namespace FinanceDesk.Models;

public class PaymentVM
{
    [Required, Display(Name = "Student")]
    public int? StudentId { get; set; }

    [Display(Name = "Invoice (leave blank to auto-match oldest unpaid)")]
    public int? InvoiceId { get; set; }

    [Required, Range(0.01, 10000000)]
    public decimal Amount { get; set; }

    [DataType(DataType.Date), DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Payment Date")]
    public DateTime PaymentDate { get; set; } = DateTime.Today;

    [Required] public string Method { get; set; } = "Cash";

    [Display(Name = "Reference No.")] public string ReferenceNo { get; set; }
    public string Remarks { get; set; }
}

public class BatchVM
{
    [Display(Name = "Grade / Level")] public string GradeLevel { get; set; }
    [Required, Display(Name = "Fee")] public int FeeId { get; set; }
    [Display(Name = "Discount")] public int? DiscountId { get; set; }

    [DataType(DataType.Date), DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Issue Date")] public DateTime IssueDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date), DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Due Date")] public DateTime DueDate { get; set; } = DateTime.Today.AddDays(30);

    [Range(1, 12)] public int Installments { get; set; } = 1;
}
