using System.ComponentModel.DataAnnotations;

namespace Chatex.Web.Models
{
    public class SignInRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email")]
        [MaxLength(256, ErrorMessage = "Maximum length of email is 256")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Minimum length of password is 6")]
        [MaxLength(32, ErrorMessage = "Maxumim length of password is 32")]
        public required string Password { get; set; }
    }
}
