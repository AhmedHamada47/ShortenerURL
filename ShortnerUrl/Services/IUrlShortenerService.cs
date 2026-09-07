using ShortnerUrl.Dtos;

namespace ShortnerUrl.Services
{
    public interface IUrlShortenerService
    {
        Task<ShortUrlResponse> CreateShortUrlAsync(CreateShortUrlRequest request, string baseUrl);
        Task<ShortUrlResponse?> GetByCodeAsync(string code);
        Task<string?> RedirectAsync(string code, string? referrer, string? userAgent, string? ipAddress);
        Task<List<ShortUrlResponse>> ListAllAsync(string baseUrl, int page = 1, int pageSize = 20);
        Task<bool> DeleteAsync(string code);
        Task<UrlStatsResponse?> GetStatsAsync(string code);
        Task<byte[]?> GetQrCodeAsync(string code, string baseUrl);
        byte[] GenerateQrCode(string url, int size = 10, string? foregroundHex = null, string? backgroundHex = null);
        Task<List<BulkResult>> BulkShortenAsync(BulkShortenRequest request, string baseUrl);
        Task<string> CreateApiKeyAsync(string workspaceName);
        Task<ShortUrlResponse?> UpdateUrlAsync(string code, UpdateUrlRequest request, string baseUrl);
    }
}
