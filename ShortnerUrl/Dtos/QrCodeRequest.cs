namespace ShortnerUrl.Dtos
{
    public class QrCodeRequest
    {
        public string Url { get; set; } = string.Empty;
        public int Size { get; set; } = 10;
        public string? ForegroundColor { get; set; }
        public string? BackgroundColor { get; set; }
    }
}
