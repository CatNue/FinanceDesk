using System.ComponentModel.DataAnnotations;

namespace FinanceDesk.Models;

public class Student
{
    public int Id { get; set; }

    [Required, StringLength(30), Display(Name = "Student No.")]
    public string StudentNo { get; set; }

    [Required, StringLength(120), Display(Name = "Full Name")]
    public string FullName { get; set; }

    [EmailAddress, StringLength(120)]
    public string Email { get; set; }

    [StringLength(30)]
    public string Phone { get; set; }

    [Required, StringLength(30), Display(Name = "Grade / Level")]
    public string GradeLevel { get; set; }

    public List<Invoice> Invoices { get; set; } = new();
}
