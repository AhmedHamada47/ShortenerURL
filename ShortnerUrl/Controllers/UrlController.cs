using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> Shorten([FromBody] CreateShortUrlRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
                return BadRequest(new { error = "URL is required." });

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var result = await _service.CreateShortUrlAsync(request, baseUrl);
            return Ok(result);
        }

        [HttpGet("list")]
        public async Task<IActionResult> ListUrls()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var urls = await _service.ListAllAsync(baseUrl);
            return Ok(urls);
        }

        [HttpDelete("{code}")]
        public async Task<IActionResult> Delete(string code)
        {
            var deleted = await _service.DeleteAsync(code);
            if (!deleted)
                return NotFound(new { error = "Short URL not found." });

            return NoContent();
        }

        [HttpGet("urls/{code}/stats")]
        public async Task<IActionResult> GetStats(string code)
        {
            var stats = await _service.GetStatsAsync(code);
            if (stats == null)
                return NotFound(new { error = "Short URL not found." });

            return Ok(stats);
        }

        [HttpGet("urls/{code}/qr")]
        public async Task<IActionResult> GetQrCode(string code)
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var qrBytes = await _service.GetQrCodeAsync(code, baseUrl);
            if (qrBytes == null)
                return NotFound(new { error = "Short URL not found." });

            return File(qrBytes, "image/png");
        }

        [HttpPost("shorten/bulk")]
        public async Task<IActionResult> BulkShorten([FromBody] BulkShortenRequest request)
        {
            if (request.Items == null || request.Items.Count == 0)
                return BadRequest(new { error = "At least one URL is required." });

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var results = await _service.BulkShortenAsync(request, baseUrl);
            return Ok(new BulkShortenResponse { Results = results });
        }
    }
}
