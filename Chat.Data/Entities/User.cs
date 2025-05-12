using System.ComponentModel.DataAnnotations;

namespace Chat.Data.Entities
{
    public class User
    {
        public Guid Id { get; set; }

        [MaxLength(255)]
        public required string Name { get; set; }

        [MaxLength(255)]
        public required string Email { get; set; }

        [MaxLength(2048)]
        public required string PasswordHash { get; set; }

        [MaxLength(255)]
        public string? Avatar { get; set; }
        public DateTime Created { get; set; }
    }
}
