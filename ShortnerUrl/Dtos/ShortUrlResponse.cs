namespace ShortnerUrl.Dtos
{
    public class ShortUrlResponse
    {
        public int Id { get; set; }
        public string LongUrl { get; set; } = string.Empty;
        public string ShortUrl { get; set; } = string.Empty;
        public string ShortCode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public int Clicks { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? CustomAlias { get; set; }
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    }
}
