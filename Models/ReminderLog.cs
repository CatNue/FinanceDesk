namespace FinanceDesk.Models;

public class ReminderLog
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; }
    public string Channel { get; set; }   // Email / SMS
    public string Message { get; set; }
    public string Result { get; set; }
    public DateTime SentAt { get; set; } = DateTime.Now;
}
