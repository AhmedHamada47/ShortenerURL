using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShortnerUrl.Dtos;
using ShortnerUrl.Services;

namespace ShortnerUrl.Controllers
{
    [ApiController]
    [Route("api")]
    public class UrlController : ControllerBase
    {
        private readonly IUrlShortenerService _service;
        private readonly ILogger<UrlController> _logger;

        public UrlController(IUrlShortenerService service, ILogger<UrlController> logger)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("shorten")]
        [EnableRateLimiting("CreateRateLimit")]
        public async Task<IActionResult> Shorten([FromBody] CreateShortUrlRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
                return BadRequest(new { error = "URL is required." });

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var result = await _service.CreateShortUrlAsync(request, baseUrl);
            return Ok(result);
        }

        [HttpGet("list")]
        [EnableRateLimiting("ReadRateLimit")]
        public async Task<IActionResult> ListUrls([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var urls = await _service.ListAllAsync(baseUrl, page, pageSize);
            return Ok(urls);
        }

        [HttpGet("health")]
        public IActionResult Health() => Ok(new { status = "healthy", timestamp = DateTime.UtcNow });

        [HttpDelete("{code}")]
        public async Task<IActionResult> Delete(string code)
        {
            var deleted = await _service.DeleteAsync(code);
            if (!deleted)
                return NotFound(new { error = "Short URL not found." });

            return NoContent();
        }

        [HttpPut("urls/{code}")]
        public async Task<IActionResult> UpdateUrl(string code, [FromBody] UpdateUrlRequest request)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var result = await _service.UpdateUrlAsync(code, request, baseUrl);
            if (result == null)
                return NotFound(new { error = "Short URL not found." });

            return Ok(result);
        }

        [HttpGet("urls/{code}/stats")]
        [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Client)]
        [EnableRateLimiting("ReadRateLimit")]
        public async Task<IActionResult> GetStats(string code)
        {
            var stats = await _service.GetStatsAsync(code);
            if (stats == null)
                return NotFound(new { error = "Short URL not found." });

            return Ok(stats);
        }

        [HttpGet("urls/{code}/qr")]
        [EnableRateLimiting("ReadRateLimit")]
        public async Task<IActionResult> GetQrCode(string code)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var qrBytes = await _service.GetQrCodeAsync(code, baseUrl);
            if (qrBytes == null)
                return NotFound(new { error = "Short URL not found." });

            Response.Headers["Cache-Control"] = "public, max-age=86400";
            return File(qrBytes, "image/png");
        }

        [HttpPost("qr")]
        [EnableRateLimiting("CreateRateLimit")]
        public IActionResult GenerateQrCode([FromBody] QrCodeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
                return BadRequest(new { error = "URL is required." });

            var validation = UrlValidator.ValidateUrl(request.Url);
            if (!validation.IsValid)
                return BadRequest(new { error = validation.Error });

            var qrBytes = _service.GenerateQrCode(request.Url, request.Size, request.ForegroundColor, request.BackgroundColor);
            return File(qrBytes, "image/png");
        }

        [HttpPost("shorten/bulk")]
        [EnableRateLimiting("CreateRateLimit")]
        public async Task<IActionResult> BulkShorten([FromBody] BulkShortenRequest request)
        {
            if (request.Items == null || request.Items.Count == 0)
                return BadRequest(new { error = "At least one URL is required." });

            if (request.Items.Count > 100)
                return BadRequest(new { error = "Bulk requests are limited to 100 URLs." });

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var results = await _service.BulkShortenAsync(request, baseUrl);
            return Ok(new BulkShortenResponse { Results = results });
        }
    }
}
