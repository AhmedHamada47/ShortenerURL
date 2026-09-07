using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using ShortnerUrl.Data;
using ShortnerUrl.Dtos;
using ShortnerUrl.Exceptions;
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
                throw new Exceptions.ValidationException(validation.Error!);
            }

            var aliasValidation = UrlValidator.ValidateCustomAlias(request.CustomAlias);
            if (!aliasValidation.IsValid)
            {
                _logger.LogWarning("Custom alias validation failed: {Error}", aliasValidation.Error);
                throw new Exceptions.ValidationException(aliasValidation.Error!);
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
                    throw new ConflictException("This custom alias is already taken.");
                }

                if (existing != null)
                {
                    _logger.LogInformation("URL {Url} already shortened as {Code}, creating new alias {Alias}",
                        request.Url, existing.ShortCode, request.CustomAlias);
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
                CustomAlias = request.CustomAlias,
                IsActive = true
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

            if (!url.IsActive)
            {
                _logger.LogInformation("Inactive link accessed: {Code}", code);
                return "EXPIRED";
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
                Referrer = Truncate(referrer, 2048),
                UserAgent = Truncate(userAgent, 512),
                IpHash = ipAddress != null ? HashIp(ipAddress) : null
            };
            _context.UrlClicks.Add(click);

            url.Clicks++;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Redirect hit: {Code} -> {LongUrl}", code, url.LongUrl);

            return url.LongUrl;
        }

        public async Task<List<ShortUrlResponse>> ListAllAsync(string baseUrl, int page = 1, int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var urls = await _context.UrlShorteners
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
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

        public async Task<ShortUrlResponse?> UpdateUrlAsync(string code, UpdateUrlRequest request, string baseUrl)
        {
            var url = await _context.UrlShorteners.FirstOrDefaultAsync(x => x.ShortCode == code);
            if (url == null) return null;

            if (!string.IsNullOrWhiteSpace(request.Url))
            {
                var validation = UrlValidator.ValidateUrl(request.Url);
                if (!validation.IsValid)
                    throw new Exceptions.ValidationException(validation.Error!);
                url.LongUrl = request.Url;
            }

            if (request.ExpiresAt.HasValue)
            {
                var exp = request.ExpiresAt.Value;
                if (exp.Kind == DateTimeKind.Unspecified)
                    exp = DateTime.SpecifyKind(exp, DateTimeKind.Utc);
                url.ExpiresAt = exp;
            }

            if (request.IsActive.HasValue)
                url.IsActive = request.IsActive.Value;

            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated short URL: {Code}", code);
            return MapToResponse(url, baseUrl);
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

            var topBrowsers = url.UrlClicks
                .Where(c => !string.IsNullOrWhiteSpace(c.UserAgent))
                .GroupBy(c => ParseBrowser(c.UserAgent!))
                .Select(g => new BrowserEntry
                {
                    Browser = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(b => b.Count)
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
                IsActive = url.IsActive,
                ClicksOverTime = clicksOverTime,
                TopReferrers = topReferrers,
                TopBrowsers = topBrowsers
            };
        }

        public async Task<byte[]?> GetQrCodeAsync(string code, string baseUrl)
        {
            var url = await _context.UrlShorteners.FirstOrDefaultAsync(x => x.ShortCode == code);
            if (url == null) return null;

            var shortUrl = $"{baseUrl.TrimEnd('/')}/{code}";
            return GenerateQrCode(shortUrl);
        }

        public byte[] GenerateQrCode(string url, int size = 10, string? foregroundHex = null, string? backgroundHex = null)
        {
            size = Math.Clamp(size, 5, 30);
            var fg = ParseColor(foregroundHex, new byte[] { 0, 0, 0 });
            var bg = ParseColor(backgroundHex, new byte[] { 255, 255, 255 });

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var pngQrCode = new PngByteQRCode(qrCodeData);
            return pngQrCode.GetGraphic(size, fg, bg);
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
                catch (Exceptions.ValidationException ex)
                {
                    results.Add(new BulkResult
                    {
                        OriginalUrl = item.Url,
                        Success = false,
                        Error = ex.Message
                    });
                }
                catch (ConflictException ex)
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
            throw new ConflictException("Unable to generate a unique short code. Please try again.");
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

        private static string HashIp(string ip)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(ip);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant()[..16];
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (value == null) return null;
            return value.Length <= maxLength ? value : value[..maxLength];
        }

        private static string ParseBrowser(string userAgent)
        {
            if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase)) return "Edge";
            if (userAgent.Contains("OPR/", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Opera", StringComparison.OrdinalIgnoreCase)) return "Opera";
            if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase)) return "Chrome";
            if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase) && !userAgent.Contains("Chrome", StringComparison.OrdinalIgnoreCase)) return "Safari";
            if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase)) return "Firefox";
            return "Other";
        }

        private static byte[] ParseColor(string? hex, byte[] fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            hex = hex.TrimStart('#');
            if (hex.Length != 6 || !Regex.IsMatch(hex, "^[0-9a-fA-F]{6}$")) return fallback;
            return new[]
            {
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16)
            };
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
                CustomAlias = entity.CustomAlias,
                IsActive = entity.IsActive
            };
        }
    }
}
