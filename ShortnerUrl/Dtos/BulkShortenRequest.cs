namespace ShortnerUrl.Dtos
{
    public class BulkShortenRequest
    {
        public List<BulkItem> Items { get; set; } = new();
    }

    public class BulkItem
    {
        public string Url { get; set; } = string.Empty;
        public string? CustomAlias { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }

    public class BulkShortenResponse
    {
        public List<BulkResult> Results { get; set; } = new();
    }

    public class BulkResult
    {
        public string OriginalUrl { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ShortUrl { get; set; }
        public string? Error { get; set; }
    }
}
