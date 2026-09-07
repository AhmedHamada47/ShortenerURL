using System.Net;
using System.Text.Json;
using ShortnerUrl.Exceptions;

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
            catch (Exception ex) when (!context.Response.HasStarted)
            {
                var (statusCode, title) = ex switch
                {
                    Exceptions.ValidationException => ((int)HttpStatusCode.BadRequest, "Validation Error"),
                    ConflictException => ((int)HttpStatusCode.Conflict, "Conflict"),
                    NotFoundException => ((int)HttpStatusCode.NotFound, "Not Found"),
                    // Keep backward compatibility for ArgumentException thrown from FluentValidation or other sources
                    ArgumentException => ((int)HttpStatusCode.BadRequest, "Validation Error"),
                    InvalidOperationException => ((int)HttpStatusCode.Conflict, "Operation Error"),
                    _ => ((int)HttpStatusCode.InternalServerError, "Internal Server Error")
                };

                var detail = statusCode == 500 ? "An unexpected error occurred." : ex.Message;

                if (statusCode >= 500)
                    _logger.LogError(ex, "Unhandled exception");
                else
                    _logger.LogWarning("Request error ({StatusCode}): {Message}", statusCode, ex.Message);

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = statusCode;
                await WriteErrorResponse(context, title, detail);
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
                Instance = context.Request.Path,
                TraceId = context.TraceIdentifier
            };
            var json = JsonSerializer.Serialize(response);
            await context.Response.WriteAsync(json);
        }
    }
}
