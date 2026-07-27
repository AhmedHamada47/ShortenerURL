using System.Net;
using System.Text.Json;

namespace ShortnerUrl.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Validation error: {Message}", ex.Message);
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await WriteErrorResponse(context, "Validation Error", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning("Operation error: {Message}", ex.Message);
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                await WriteErrorResponse(context, "Operation Error", ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                await WriteErrorResponse(context, "Internal Server Error", "An unexpected error occurred.");
            }
        }

        private static async Task WriteErrorResponse(HttpContext context, string title, string detail)
        {
            var response = new
            {
                Type = "https://tools.ietf.org/html/rfc7231",
                Title = title,
                Status = context.Response.StatusCode,
                Detail = detail,
                Instance = context.Request.Path
            };
            var json = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(json);
        }
    }
}
