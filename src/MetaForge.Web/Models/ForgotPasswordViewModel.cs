using System.ComponentModel.DataAnnotations;

namespace MetaForge.Web.Models;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Email or username is required")]
    [Display(Name = "Email or username")]
    public string EmailOrUserName { get; set; } = string.Empty;

    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }
}
