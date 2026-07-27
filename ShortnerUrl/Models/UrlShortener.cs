using System.ComponentModel.DataAnnotations;

namespace ShortnerUrl.Models
{
    public class UrlShortener
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string LongUrl { get; set; } = string.Empty;

        [Required]
        public string ShortCode { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int Clicks { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public string? CustomAlias { get; set; }

        public ICollection<UrlClick> UrlClicks { get; set; } = new List<UrlClick>();
    }
}
