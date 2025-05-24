using Microsoft.AspNetCore.Mvc;
using System.Xml.Serialization;
using System;

namespace ShortnerUrl.Controllers
{
    [ApiController]
    [Route("API")]
    public class UrlController : ControllerBase
    {
        private readonly Data.AppDbContext _context;
        private IHttpContextAccessor _httpContextAccessor;
        public UrlController(Data.AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }
        [HttpPost("shorten")]
        public async Task<IActionResult> Shorten([FromBody] string Url)
        {
            var code = Guid.NewGuid().ToString().Substring(0, 6);
            var mapping = new Models.UrlShrotner
            {
                LongUrl = Url,
                ShortUrl = code
            };
            _context.UrlShrotner.Add(mapping);
            await _context.SaveChangesAsync();
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            return Ok($"{baseUrl}/{code}");

        }
        [HttpGet("list")]
        public IActionResult ListUrls()
        {
            // Get all URL mappings from the database
            var urlList = _context.UrlShrotner.ToList();

            // Format them with the base URL for display
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var formattedUrls = urlList.Select(url => new
            {
                Id = url.Id,
                LongUrl = url.LongUrl,
                ShortUrl = $"{baseUrl}/{url.ShortUrl}",
                Code = url.ShortUrl
            }).ToList();

            return Ok(formattedUrls);
        }
    }
}

