using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceDesk.Models;

public class Payment
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; }

    [Column(TypeName = "decimal(12,2)"), Range(0.01, 10000000)]
    public decimal Amount { get; set; }

    [DataType(DataType.Date), DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Payment Date")]
    public DateTime PaymentDate { get; set; } = DateTime.Today;

    [Required, StringLength(30)]
    public string Method { get; set; } = "Cash";

    [StringLength(60), Display(Name = "Reference No.")]
    public string ReferenceNo { get; set; }

    // "Reconciled" or "Flagged" (discrepancy needing review)
    [StringLength(20)]
    public string Status { get; set; } = "Reconciled";

    [StringLength(250)]
    public string Remarks { get; set; }

    [StringLength(120), Display(Name = "Received By")]
    public string ReceivedBy { get; set; }

    public Receipt Receipt { get; set; }
}
