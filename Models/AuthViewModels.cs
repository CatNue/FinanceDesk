using System.ComponentModel.DataAnnotations;

namespace FinanceDesk.Models;

public class LoginVM
{
    [Required(ErrorMessage = "Enter your username.")] public string Username { get; set; }
    [Required(ErrorMessage = "Enter your password."), DataType(DataType.Password)] public string Password { get; set; }
    [Display(Name = "Remember me")] public bool RememberMe { get; set; }
    public string ReturnUrl { get; set; }
}

public class ChangePasswordVM
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; }

    [Required, StringLength(60, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters."), DataType(DataType.Password), Display(Name = "New password")]
    public string NewPassword { get; set; }

    [Required, DataType(DataType.Password), Compare("NewPassword", ErrorMessage = "Passwords do not match."), Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; }
}

public class UserCreateVM
{
    [Required, StringLength(40, MinimumLength = 3), RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Letters, numbers, dot, dash and underscore only.")]
    public string Username { get; set; }

    [Required, StringLength(120), Display(Name = "Full Name")]
    public string FullName { get; set; }

    [StringLength(120), EmailAddress]
    public string Email { get; set; }

    [Required] public string Role { get; set; } = "Cashier";

    [Required, StringLength(60, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters."), DataType(DataType.Password)]
    public string Password { get; set; }

    [Required, DataType(DataType.Password), Compare("Password", ErrorMessage = "Passwords do not match."), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; }
}

public class UserEditVM
{
    public int Id { get; set; }
    public string Username { get; set; }
    [Required, StringLength(120), Display(Name = "Full Name")] public string FullName { get; set; }
    [StringLength(120), EmailAddress] public string Email { get; set; }
    [Required] public string Role { get; set; }
    [Display(Name = "Account active")] public bool IsActive { get; set; }
}

public class ResetPasswordVM
{
    public int Id { get; set; }
    public string Username { get; set; }

    [Required, StringLength(60, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters."), DataType(DataType.Password), Display(Name = "New password")]
    public string NewPassword { get; set; }

    [Required, DataType(DataType.Password), Compare("NewPassword", ErrorMessage = "Passwords do not match."), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; }
}

// ---- Self-service account features ----

public class ForgotPasswordVM
{
    [Required(ErrorMessage = "Enter your username or email.")]
    [Display(Name = "Username or email")]
    public string UsernameOrEmail { get; set; }
}

public class ResetPasswordPublicVM
{
    [Required] public string Token { get; set; }

    [Required, StringLength(60, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters."), DataType(DataType.Password), Display(Name = "New password")]
    public string NewPassword { get; set; }

    [Required, DataType(DataType.Password), Compare("NewPassword", ErrorMessage = "Passwords do not match."), Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; }
}

public class ProfileVM
{
    public string Username { get; set; }
    public string Role { get; set; }
    [Required, StringLength(120), Display(Name = "Full Name")] public string FullName { get; set; }
    [StringLength(120), EmailAddress, Display(Name = "Email address")] public string Email { get; set; }
}

public class DeleteAccountVM
{
    [Required, DataType(DataType.Password), Display(Name = "Confirm your password")]
    public string Password { get; set; }
}
