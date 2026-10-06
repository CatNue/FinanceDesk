using System.ComponentModel.DataAnnotations;

namespace FinanceDesk.Models;

public class User
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string Username { get; set; }

    [StringLength(120), EmailAddress]
    public string Email { get; set; }

    [Required, StringLength(120), Display(Name = "Full Name")]
    public string FullName { get; set; }

    [StringLength(250)]
    public string PasswordHash { get; set; }

    // "Admin" or "Cashier"
    [Required, StringLength(20)]
    public string Role { get; set; } = "Cashier";

    public bool IsActive { get; set; } = true;
    public int FailedAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime? LastLogin { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Forgot-password flow
    [StringLength(100)]
    public string ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }
}
