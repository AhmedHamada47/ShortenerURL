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

        public string? Referrer { get; set; }

        public string? UserAgent { get; set; }

        public string? IpAddress { get; set; }

        [ForeignKey(nameof(UrlShortenerId))]
        public UrlShortener UrlShortener { get; set; } = null!;
    }
}
