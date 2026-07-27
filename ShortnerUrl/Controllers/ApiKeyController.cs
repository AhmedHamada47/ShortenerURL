using Microsoft.AspNetCore.Mvc;
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

        public ApiKeyController(IUrlShortenerService service, ILogger<ApiKeyController> logger)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateKey([FromBody] CreateKeyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.WorkspaceName))
                return BadRequest(new { error = "Workspace name is required." });

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
