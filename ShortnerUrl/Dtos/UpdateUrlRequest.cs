namespace ShortnerUrl.Dtos
{
    public class UpdateUrlRequest
    {
        public string? Url { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public bool? IsActive { get; set; }
    }
}
