using Microsoft.EntityFrameworkCore;
using ShortnerUrl.Data;
using ShortnerUrl.Services;

namespace ShortnerUrl.Middleware
{
    public class ApiKeyAuthMiddleware
    {
        private readonly RequestDelegate _next;

        private static readonly HashSet<string> PublicPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "/api/health"
        };

        public ApiKeyAuthMiddleware(RequestDelegate next)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

            var isRedirect = context.Request.Method == "GET" && !path.StartsWith("/api/");

            var isSwagger = path.StartsWith("/swagger");
            var isStatic = path.StartsWith("/css") || path.StartsWith("/js") || path == "/" || string.IsNullOrEmpty(path) || path == "/index.html";
            var isPublicApi = path.StartsWith("/api/health");

            if (isRedirect || isSwagger || isStatic || isPublicApi)
            {
                await _next(context);
                return;
            }

            if (path.StartsWith("/api/"))
            {
                if (!context.Request.Headers.TryGetValue("X-Api-Key", out var extractedApiKey) || string.IsNullOrWhiteSpace(extractedApiKey))
                {
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"error\":\"API key is required. Provide it via the X-Api-Key header.\"}");
                    return;
                }

                var hash = UrlShortenerService.HashKey(extractedApiKey!);
                var keyExists = await dbContext.ApiKeys.AnyAsync(k => k.KeyHash == hash && k.IsActive);

                if (!keyExists)
                {
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync("{\"error\":\"Invalid or inactive API key.\"}");
                    return;
                }
            }

            await _next(context);
        }
    }
}
