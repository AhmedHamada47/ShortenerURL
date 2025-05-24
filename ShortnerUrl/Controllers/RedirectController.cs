using Microsoft.AspNetCore.Mvc;
using ShortnerUrl.Data;

namespace ShortnerUrl.Controllers
{
    [ApiController]
    [Route("")]  // This makes the controller handle the root path
    public class RedirectController : ControllerBase
    {
        private readonly AppDbContext context;
        public RedirectController(AppDbContext context)
        {
            this.context = context;
        }

        [HttpGet("{code}")]
        public IActionResult RedirectToLongUrl(string code)
        {
            var url = context.UrlShrotner.FirstOrDefault(x => x.ShortUrl == code);
            if (url != null)
                return Redirect(url.LongUrl);
            else
                return NotFound("Url not found");
        }
    }
}