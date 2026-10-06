using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceDesk.Models;

public class Discount
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; }

    // "Percentage" or "Fixed"
    [Required, StringLength(20)]
    public string Type { get; set; } = "Percentage";

    [Column(TypeName = "decimal(12,2)"), Range(0, 10000000)]
    public decimal Value { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [NotMapped]
    public string Display => Type == "Percentage" ? $"{Value:0.##}%" : Value.Money();
}
