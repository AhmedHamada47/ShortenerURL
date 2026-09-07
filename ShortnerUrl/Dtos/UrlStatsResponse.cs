namespace ShortnerUrl.Dtos
{
    public class UrlStatsResponse
    {
        public int Id { get; set; }
        public string ShortCode { get; set; } = string.Empty;
        public string LongUrl { get; set; } = string.Empty;
        public int TotalClicks { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool IsExpired { get; set; }
        public bool IsActive { get; set; }
        public List<DailyClickBucket> ClicksOverTime { get; set; } = new();
        public List<ReferrerEntry> TopReferrers { get; set; } = new();
        public List<BrowserEntry> TopBrowsers { get; set; } = new();
    }

    public class DailyClickBucket
    {
        public string Date { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class ReferrerEntry
    {
        public string Referrer { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class BrowserEntry
    {
        public string Browser { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
