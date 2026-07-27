using ShortnerUrl.Dtos;

namespace ShortnerUrl.Services
{
    public interface IUrlShortenerService
    {
        Task<ShortUrlResponse> CreateShortUrlAsync(CreateShortUrlRequest request, string baseUrl);
        Task<ShortUrlResponse?> GetByCodeAsync(string code);
        Task<string?> RedirectAsync(string code, string? referrer, string? userAgent, string? ipAddress);
        Task<List<ShortUrlResponse>> ListAllAsync(string baseUrl);
        Task<bool> DeleteAsync(string code);
        Task<UrlStatsResponse?> GetStatsAsync(string code);
        Task<byte[]?> GetQrCodeAsync(string code, string baseUrl);
        Task<List<BulkResult>> BulkShortenAsync(BulkShortenRequest request, string baseUrl);
        Task<string> CreateApiKeyAsync(string workspaceName);
    }
}
