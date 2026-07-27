namespace ShortnerUrl.Dtos
{
    public class CreateApiKeyResponse
    {
        public string PlainTextKey { get; set; } = string.Empty;
        public string WorkspaceName { get; set; } = string.Empty;
        public string Message { get; set; } = "Store this key securely — it will not be shown again.";
    }
}
