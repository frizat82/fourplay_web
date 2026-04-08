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
        public DateTimeOffset Expires { get; set; }
        [Required]
        public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? Revoked { get; set; }
        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; } = null!;
    }
}

