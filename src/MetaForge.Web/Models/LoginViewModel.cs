using System.ComponentModel.DataAnnotations;

namespace MetaForge.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Validation_UsernameRequired")]
    [Display(Name = "Auth_Username")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Validation_PasswordRequired")]
    [DataType(DataType.Password)]
    [Display(Name = "Auth_Password")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public string? SuccessMessage { get; set; }
}
