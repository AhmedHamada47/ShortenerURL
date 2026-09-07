using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShortnerUrl.Dtos;
using ShortnerUrl.Services;

namespace ShortnerUrl.Controllers
{
    [ApiController]
    [Route("api/keys")]
    public class ApiKeyController : ControllerBase
    {
        private readonly IUrlShortenerService _service;
        private readonly ILogger<ApiKeyController> _logger;
        private readonly IConfiguration _configuration;

        public ApiKeyController(IUrlShortenerService service, ILogger<ApiKeyController> logger, IConfiguration configuration)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [HttpPost("create")]
        [EnableRateLimiting("CreateRateLimit")]
        public async Task<IActionResult> CreateKey([FromBody] CreateKeyRequest request)
        {
            var adminSecret = _configuration["AdminSecret"];
            if (!string.IsNullOrWhiteSpace(adminSecret))
            {
                if (!Request.Headers.TryGetValue("X-Admin-Secret", out var provided) ||
                    provided.ToString() != adminSecret)
                {
                    return Unauthorized(new { error = "Admin secret required to create API keys." });
                }
            }

            if (string.IsNullOrWhiteSpace(request.WorkspaceName))
                return BadRequest(new { error = "Workspace name is required." });

            if (request.WorkspaceName.Length > 100)
                return BadRequest(new { error = "Workspace name must not exceed 100 characters." });

            var plainTextKey = await _service.CreateApiKeyAsync(request.WorkspaceName);

            return Ok(new CreateApiKeyResponse
            {
                PlainTextKey = plainTextKey,
                WorkspaceName = request.WorkspaceName
            });
        }
    }

    public class CreateKeyRequest
    {
        public string WorkspaceName { get; set; } = string.Empty;
    }
}
