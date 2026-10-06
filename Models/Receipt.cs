using System.ComponentModel.DataAnnotations;

namespace FinanceDesk.Models;

public class Receipt
{
    public int Id { get; set; }
    [StringLength(40)]
    public string ReceiptNo { get; set; }
    public int PaymentId { get; set; }
    public Payment Payment { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.Now;
    public int PrintCount { get; set; }
    public DateTime? LastPrintedAt { get; set; }
}
