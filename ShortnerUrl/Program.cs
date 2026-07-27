using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using ShortnerUrl.Data;
using ShortnerUrl.Middleware;
using ShortnerUrl.Services;
using ShortnerUrl.Validators;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUrlShortenerService, UrlShortenerService>();

builder.Services.AddControllers();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<CreateShortUrlRequestValidator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "ShortnerUrl.xml"), true);
    options.SwaggerDoc("v1", new()
    {
        Title = "URL Shortener Pro API",
        Version = "v1",
        Description = "A secure, feature-rich URL shortening service."
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("CreateRateLimit", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseStaticFiles();
app.UseHttpsRedirection();

app.UseMiddleware<ApiKeyAuthMiddleware>();

app.MapControllers();

app.MapFallbackToFile("index.html");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var existingDevKey = db.ApiKeys.FirstOrDefault(k => k.WorkspaceName == "Development");
    if (existingDevKey == null)
    {
        var devKey = $"usp_dev_{Guid.NewGuid():N}";
        db.ApiKeys.Add(new ShortnerUrl.Models.ApiKey
        {
            KeyHash = UrlShortenerService.HashKey(devKey),
            WorkspaceName = "Development",
            IsActive = true
        });
        db.SaveChanges();
        logger.LogWarning("========== Development API key: {Key} ==========", devKey);
    }
    else
    {
        logger.LogWarning("========== Dev API key already exists. Create a new one: POST /api/keys/create ==========");
    }
}

app.Run();
