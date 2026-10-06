using System.ComponentModel.DataAnnotations;

namespace FinanceDesk.Models;

public class AuditLog
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    [StringLength(60)] public string Username { get; set; }
    [StringLength(30)] public string Action { get; set; }   // Created / Updated / Deleted / Login / Logout / LoginFailed
    [StringLength(40)] public string Entity { get; set; }
    [StringLength(500)] public string Details { get; set; }
}
