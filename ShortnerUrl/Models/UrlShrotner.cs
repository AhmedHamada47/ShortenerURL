using System.ComponentModel.DataAnnotations;

namespace ShortnerUrl.Models
{
    public class UrlShrotner
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string LongUrl { get; set; } = string.Empty;
        [Required]
        public string ShortUrl { get; set; } = string.Empty;
    }
}
