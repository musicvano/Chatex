using System.ComponentModel.DataAnnotations;

namespace Chatex.Web.Models
{
    public class SignupRequest
    {
        [Required(ErrorMessage = "Email is required")]
        [MaxLength(256, ErrorMessage = "Maximum length of email is 256")]
        [EmailAddress(ErrorMessage = "Invalid email")]
        public required string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Minimum length of password is 6")]
        [MaxLength(32, ErrorMessage = "Maxumim length of password is 32")]
        public required string Password { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [MinLength(2, ErrorMessage = "Minimum length of name is 2")]
        [MaxLength(64, ErrorMessage = "Maximum length of name is 64")]
        public required string Name { get; set; }        
    }
}
