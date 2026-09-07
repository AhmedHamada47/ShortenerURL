using System.ComponentModel.DataAnnotations;

namespace ShortnerUrl.Models
{
    public class UrlShortener
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(2048)]
        public string LongUrl { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string ShortCode { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int Clicks { get; set; }

        public DateTime? ExpiresAt { get; set; }

        [MaxLength(30)]
        public string? CustomAlias { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<UrlClick> UrlClicks { get; set; } = new List<UrlClick>();
    }
}
