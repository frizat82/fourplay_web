using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FourPlayWebApp.Server.Models.Identity
{
    public class RefreshToken
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        [Required]
        public string UserId { get; set; } = null!;
        [Required]
        public string Token { get; set; } = null!;
        [Required]
        public DateTime Expires { get; set; }
        [Required]
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime? Revoked { get; set; }
        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; } = null!;
    }
}

