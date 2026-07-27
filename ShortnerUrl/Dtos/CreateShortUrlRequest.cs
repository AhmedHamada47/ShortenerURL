namespace ShortnerUrl.Dtos
{
    public class CreateShortUrlRequest
    {
        public string Url { get; set; } = string.Empty;
        public string? CustomAlias { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
