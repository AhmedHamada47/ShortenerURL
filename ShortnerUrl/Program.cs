using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Serilog;
using ShortnerUrl.Data;
using ShortnerUrl.Middleware;
using ShortnerUrl.Services;
using ShortnerUrl.Validators;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .Enrich.FromLogContext()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .Enrich.FromLogContext());

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<IUrlShortenerService, UrlShortenerService>();

    builder.Services.AddControllers();
    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<CreateShortUrlRequestValidator>();

    builder.Services.AddResponseCaching();
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
    });

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
        options.AddPolicy("ReadRateLimit", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseRateLimiter();
    app.UseCors();
    app.UseResponseCaching();
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
}
catch (System.IO.IOException ex) when (ex.InnerException is Microsoft.AspNetCore.Connections.AddressInUseException
                                       || ex.InnerException is System.Net.Sockets.SocketException)
{
    Log.Error(ex, "Failed to bind to configured address. It may already be in use.");
    Environment.Exit(1);
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    Environment.Exit(1);
}
finally
{
    Log.CloseAndFlush();
}
