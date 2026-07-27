using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using ShortnerUrl.Data;
using ShortnerUrl.Dtos;
using ShortnerUrl.Models;

namespace ShortnerUrl.Services
{
    public class UrlShortenerService : IUrlShortenerService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UrlShortenerService> _logger;
        private const int MaxCollisionRetries = 5;

        public UrlShortenerService(AppDbContext context, ILogger<UrlShortenerService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ShortUrlResponse> CreateShortUrlAsync(CreateShortUrlRequest request, string baseUrl)
        {
            var validation = UrlValidator.ValidateUrl(request.Url);
            if (!validation.IsValid)
            {
                _logger.LogWarning("URL validation failed: {Error}", validation.Error);
                throw new ArgumentException(validation.Error);
            }

            var aliasValidation = UrlValidator.ValidateCustomAlias(request.CustomAlias);
            if (!aliasValidation.IsValid)
            {
                _logger.LogWarning("Custom alias validation failed: {Error}", aliasValidation.Error);
                throw new ArgumentException(aliasValidation.Error);
            }

            var existing = await _context.UrlShorteners.FirstOrDefaultAsync(x => x.LongUrl == request.Url);
            if (existing != null && string.IsNullOrWhiteSpace(request.CustomAlias))
            {
                _logger.LogInformation("Returning existing short URL for {Url}", request.Url);
                return MapToResponse(existing, baseUrl);
            }

            string code;
            if (!string.IsNullOrWhiteSpace(request.CustomAlias))
            {
                var aliasExists = await _context.UrlShorteners.AnyAsync(x => x.ShortCode == request.CustomAlias);
                if (aliasExists)
                {
                    _logger.LogWarning("Custom alias {Alias} is already taken", request.CustomAlias);
                    throw new ArgumentException("This custom alias is already taken.");
                }
                code = request.CustomAlias;
            }
            else
            {
                code = await GenerateUniqueCodeAsync();
            }

            if (request.ExpiresAt.HasValue && request.ExpiresAt.Value.Kind == DateTimeKind.Unspecified)
            {
                request.ExpiresAt = DateTime.SpecifyKind(request.ExpiresAt.Value, DateTimeKind.Utc);
            }

            var mapping = new UrlShortener
            {
                LongUrl = request.Url,
                ShortCode = code,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = request.ExpiresAt,
                CustomAlias = request.CustomAlias
            };

            _context.UrlShorteners.Add(mapping);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created short URL: {Code} -> {LongUrl}", code, request.Url);

            return MapToResponse(mapping, baseUrl);
        }

        public async Task<ShortUrlResponse?> GetByCodeAsync(string code)
        {
            var entity = await _context.UrlShorteners.FirstOrDefaultAsync(x => x.ShortCode == code);
            if (entity == null) return null;

            return MapToResponse(entity, null);
        }

        public async Task<string?> RedirectAsync(string code, string? referrer, string? userAgent, string? ipAddress)
        {
            var url = await _context.UrlShorteners.FirstOrDefaultAsync(x => x.ShortCode == code);
            if (url == null)
            {
                _logger.LogWarning("Redirect not found for code: {Code}", code);
                return null;
            }

            if (url.ExpiresAt.HasValue && DateTime.UtcNow > url.ExpiresAt.Value)
            {
                _logger.LogInformation("Expired link accessed: {Code}", code);
                return "EXPIRED";
            }

            var click = new UrlClick
            {
                UrlShortenerId = url.Id,
                ClickedAt = DateTime.UtcNow,
                Referrer = referrer,
                UserAgent = userAgent,
                IpAddress = ipAddress
            };
            _context.UrlClicks.Add(click);

            url.Clicks++;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Redirect hit: {Code} -> {LongUrl}", code, url.LongUrl);

            return url.LongUrl;
        }

        public async Task<List<ShortUrlResponse>> ListAllAsync(string baseUrl)
        {
            var urls = await _context.UrlShorteners.OrderByDescending(x => x.CreatedAt).ToListAsync();
            return urls.Select(u => MapToResponse(u, baseUrl)).ToList();
        }

        public async Task<bool> DeleteAsync(string code)
        {
            var url = await _context.UrlShorteners.FirstOrDefaultAsync(x => x.ShortCode == code);
            if (url == null) return false;

            _context.UrlShorteners.Remove(url);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted short URL: {Code}", code);
            return true;
        }

        public async Task<UrlStatsResponse?> GetStatsAsync(string code)
        {
            var url = await _context.UrlShorteners.Include(x => x.UrlClicks).FirstOrDefaultAsync(x => x.ShortCode == code);
            if (url == null) return null;

            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

            var clicksOverTime = url.UrlClicks
                .Where(c => c.ClickedAt >= thirtyDaysAgo)
                .GroupBy(c => c.ClickedAt.Date)
                .Select(g => new DailyClickBucket
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Count = g.Count()
                })
                .OrderBy(d => d.Date)
                .ToList();

            var topReferrers = url.UrlClicks
                .Where(c => !string.IsNullOrWhiteSpace(c.Referrer))
                .GroupBy(c => c.Referrer!)
                .Select(g => new ReferrerEntry
                {
                    Referrer = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(r => r.Count)
                .Take(10)
                .ToList();

            return new UrlStatsResponse
            {
                Id = url.Id,
                ShortCode = url.ShortCode,
                LongUrl = url.LongUrl,
                TotalClicks = url.Clicks,
                CreatedAt = url.CreatedAt,
                ExpiresAt = url.ExpiresAt,
                IsExpired = url.ExpiresAt.HasValue && DateTime.UtcNow > url.ExpiresAt.Value,
                ClicksOverTime = clicksOverTime,
                TopReferrers = topReferrers
            };
        }

        public async Task<byte[]?> GetQrCodeAsync(string code, string baseUrl)
        {
            var url = await _context.UrlShorteners.FirstOrDefaultAsync(x => x.ShortCode == code);
            if (url == null) return null;

            var shortUrl = $"{baseUrl.TrimEnd('/')}/{code}";

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(shortUrl, QRCodeGenerator.ECCLevel.Q);
            using var pngQrCode = new PngByteQRCode(qrCodeData);
            var qrBytes = pngQrCode.GetGraphic(20);

            _logger.LogInformation("QR code generated for {Code}", code);
            return qrBytes;
        }

        public async Task<List<BulkResult>> BulkShortenAsync(BulkShortenRequest request, string baseUrl)
        {
            var results = new List<BulkResult>();
            foreach (var item in request.Items)
            {
                try
                {
                    var createRequest = new CreateShortUrlRequest
                    {
                        Url = item.Url,
                        CustomAlias = item.CustomAlias,
                        ExpiresAt = item.ExpiresAt
                    };
                    var response = await CreateShortUrlAsync(createRequest, baseUrl);
                    results.Add(new BulkResult
                    {
                        OriginalUrl = item.Url,
                        Success = true,
                        ShortUrl = response.ShortUrl
                    });
                }
                catch (ArgumentException ex)
                {
                    results.Add(new BulkResult
                    {
                        OriginalUrl = item.Url,
                        Success = false,
                        Error = ex.Message
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Bulk shorten failed for {Url}", item.Url);
                    results.Add(new BulkResult
                    {
                        OriginalUrl = item.Url,
                        Success = false,
                        Error = "An unexpected error occurred."
                    });
                }
            }

            return results;
        }

        public async Task<string> CreateApiKeyAsync(string workspaceName)
        {
            var plainTextKey = GenerateApiKey();
            var hash = HashKey(plainTextKey);

            var apiKey = new ApiKey
            {
                KeyHash = hash,
                WorkspaceName = workspaceName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.ApiKeys.Add(apiKey);
            await _context.SaveChangesAsync();

            _logger.LogInformation("API key created for workspace: {Workspace}", workspaceName);
            return plainTextKey;
        }

        private async Task<string> GenerateUniqueCodeAsync()
        {
            for (int attempt = 1; attempt <= MaxCollisionRetries; attempt++)
            {
                var code = CodeGenerator.GenerateCryptographicCode();
                var exists = await _context.UrlShorteners.AnyAsync(x => x.ShortCode == code);
                if (!exists)
                {
                    if (attempt > 1)
                    {
                        _logger.LogWarning("Collision resolved after {Attempt} attempt(s)", attempt);
                    }
                    return code;
                }
                _logger.LogWarning("Collision detected on attempt {Attempt}/{MaxRetries}", attempt, MaxCollisionRetries);
            }

            _logger.LogError("Failed to generate unique code after {MaxRetries} attempts", MaxCollisionRetries);
            throw new InvalidOperationException("Unable to generate a unique short code. Please try again.");
        }

        private static string GenerateApiKey()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return $"usp_{Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
        }

        internal static string HashKey(string key)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(key);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static ShortUrlResponse MapToResponse(UrlShortener entity, string? baseUrl)
        {
            return new ShortUrlResponse
            {
                Id = entity.Id,
                LongUrl = entity.LongUrl,
                ShortCode = entity.ShortCode,
                ShortUrl = baseUrl != null ? $"{baseUrl.TrimEnd('/')}/{entity.ShortCode}" : entity.ShortCode,
                CreatedAt = entity.CreatedAt,
                Clicks = entity.Clicks,
                ExpiresAt = entity.ExpiresAt,
                CustomAlias = entity.CustomAlias
            };
        }
    }
}
