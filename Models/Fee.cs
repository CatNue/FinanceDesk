using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceDesk.Models;

public class Fee
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; }

    [Required, StringLength(40)]
    public string Category { get; set; } = "Tuition";

    [Column(TypeName = "decimal(12,2)"), Range(0, 10000000)]
    public decimal Amount { get; set; }

    [StringLength(250)]
    public string Description { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
