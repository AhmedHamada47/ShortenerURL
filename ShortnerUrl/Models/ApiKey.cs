using System.ComponentModel.DataAnnotations;

namespace ShortnerUrl.Models
{
    public class ApiKey
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string KeyHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string WorkspaceName { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiresAt { get; set; }
    }
}
