using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceDesk.Models;

public class Invoice
{
    public int Id { get; set; }

    [StringLength(40), Display(Name = "Invoice No.")]
    public string InvoiceNo { get; set; }

    [Display(Name = "Student")]
    public int StudentId { get; set; }
    public Student Student { get; set; }

    [Display(Name = "Fee")]
    public int FeeId { get; set; }
    public Fee Fee { get; set; }

    [Display(Name = "Discount")]
    public int? DiscountId { get; set; }
    public Discount Discount { get; set; }

    [Column(TypeName = "decimal(12,2)"), Range(0, 10000000)]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(12,2)"), Display(Name = "Discount Amount")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(12,2)"), Range(0, 10000000), Display(Name = "Late Fee")]
    public decimal LateFee { get; set; }

    [DataType(DataType.Date), DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Issue Date")]
    public DateTime IssueDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date), DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Due Date")]
    public DateTime DueDate { get; set; } = DateTime.Today.AddDays(30);

    [Range(1, 12), Display(Name = "Installments")]
    public int Installments { get; set; } = 1;

    [StringLength(250)]
    public string Notes { get; set; }

    public bool IsArchived { get; set; }

    public List<Payment> Payments { get; set; } = new();

    // ---- Calculated (not stored) ----
    [NotMapped] public decimal Total => Amount - DiscountAmount + LateFee;
    [NotMapped] public decimal AmountPaid => Payments?.Sum(p => p.Amount) ?? 0;
    [NotMapped] public decimal Balance => Total - AmountPaid;
    [NotMapped] public decimal InstallmentAmount => Installments > 1 ? Math.Round(Total / Installments, 2) : Total;
    [NotMapped]
    public string Status =>
        Balance <= 0 ? "Paid" :
        DueDate.Date < DateTime.Today ? "Overdue" :
        AmountPaid > 0 ? "Partial" : "Unpaid";
}
