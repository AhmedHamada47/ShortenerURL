using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShortnerUrl.Models
{
    public class UrlClick
    {
        [Key]
        public int Id { get; set; }

        public int UrlShortenerId { get; set; }

        public DateTime ClickedAt { get; set; } = DateTime.UtcNow;

        [MaxLength(2048)]
        public string? Referrer { get; set; }

        [MaxLength(512)]
        public string? UserAgent { get; set; }

        [MaxLength(64)]
        public string? IpHash { get; set; }

        [ForeignKey(nameof(UrlShortenerId))]
        public UrlShortener UrlShortener { get; set; } = null!;
    }
}
