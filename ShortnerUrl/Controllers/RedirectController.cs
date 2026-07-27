using Microsoft.AspNetCore.Mvc;
using ShortnerUrl.Services;

namespace ShortnerUrl.Controllers
{
    [ApiController]
    [Route("")]
    public class RedirectController : ControllerBase
    {
        private readonly IUrlShortenerService _service;
        private readonly ILogger<RedirectController> _logger;

        public RedirectController(IUrlShortenerService service, ILogger<RedirectController> logger)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("{code}")]
        public async Task<IActionResult> RedirectToLongUrl(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return BadRequest("Code is required.");

            var referrer = Request.Headers.Referer.FirstOrDefault();
            var userAgent = Request.Headers.UserAgent.FirstOrDefault();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

            var result = await _service.RedirectAsync(code, referrer, userAgent, ipAddress);

            if (result == null)
                return NotFound("Short URL not found.");

            if (result == "EXPIRED")
                return StatusCode(410, new { error = "This link has expired.", code });

            if (!Uri.TryCreate(result, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                _logger.LogError("Invalid target URL in database for code {Code}: {Url}", code, result);
                return BadRequest("Invalid target URL configured.");
            }

            _logger.LogInformation("Redirecting {Code} to {Url}", code, result);
            return Redirect(result);
        }
    }
}
