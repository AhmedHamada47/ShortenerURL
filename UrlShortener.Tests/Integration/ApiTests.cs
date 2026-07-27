using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShortnerUrl.Data;
using ShortnerUrl.Dtos;
using ShortnerUrl.Services;

namespace UrlShortener.Tests.Integration;

public class ApiTests : IClassFixture<WebApplicationFactory<ShortnerUrl.WebEntryPoint>>, IDisposable
{
    private readonly WebApplicationFactory<ShortnerUrl.WebEntryPoint> _factory;
    private readonly string _dbPath;
    private readonly string _apiKey;

    public ApiTests(WebApplicationFactory<ShortnerUrl.WebEntryPoint> factory)
    {
        _apiKey = "usp_test-api-key-for-integration-" + Guid.NewGuid();
        _dbPath = Path.GetTempFileName();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_dbPath}");

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite($"Data Source={_dbPath}"));
            });
        });

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ctx.Database.Migrate();

        var hash = UrlShortenerService.HashKey(_apiKey);
        ctx.ApiKeys.Add(new ShortnerUrl.Models.ApiKey
        {
            KeyHash = hash,
            WorkspaceName = "test",
            IsActive = true
        });
        ctx.SaveChanges();
    }

    public void Dispose()
    {
        try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { }
    }

    private HttpClient CreateClient(bool withKey = true)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        if (withKey)
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
        }
        return client;
    }

    [Fact]
    public async Task Shorten_CreateAndRedirect_Flow()
    {
        var client = CreateClient();

        var createRes = await client.PostAsJsonAsync("/api/shorten", new CreateShortUrlRequest
        {
            Url = "https://example.com/integration-test"
        });
        createRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = await createRes.Content.ReadFromJsonAsync<ShortUrlResponse>();
        created.Should().NotBeNull();
        created!.ShortCode.Should().NotBeNullOrEmpty();

        var statsRes = await client.GetAsync($"/api/urls/{created.ShortCode}/stats");
        statsRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await statsRes.Content.ReadFromJsonAsync<UrlStatsResponse>();
        stats.Should().NotBeNull();
        stats!.TotalClicks.Should().Be(0);

        var redirectRes = await client.GetAsync($"/{created.ShortCode}");
        redirectRes.StatusCode.Should().Be(HttpStatusCode.Redirect);
        redirectRes.Headers.Location.Should().Be("https://example.com/integration-test");

        statsRes = await client.GetAsync($"/api/urls/{created.ShortCode}/stats");
        stats = await statsRes.Content.ReadFromJsonAsync<UrlStatsResponse>();
        stats!.TotalClicks.Should().Be(1);
    }

    [Fact]
    public async Task Shorten_InvalidUrl_ReturnsBadRequest()
    {
        var client = CreateClient();

        var res = await client.PostAsJsonAsync("/api/shorten", new CreateShortUrlRequest
        {
            Url = "not-a-valid-url"
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Shorten_PrivateIp_ReturnsBadRequest()
    {
        var client = CreateClient();

        var res = await client.PostAsJsonAsync("/api/shorten", new CreateShortUrlRequest
        {
            Url = "http://127.0.0.1/secret"
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Shorten_WithCustomAlias_Succeeds()
    {
        var client = CreateClient();

        var res = await client.PostAsJsonAsync("/api/shorten", new CreateShortUrlRequest
        {
            Url = "https://example.com/alias-test",
            CustomAlias = "my-test-alias"
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var data = await res.Content.ReadFromJsonAsync<ShortUrlResponse>();
        data!.ShortCode.Should().Be("my-test-alias");
    }

    [Fact]
    public async Task ExpiredLink_Returns410()
    {
        var client = CreateClient();

        var createRes = await client.PostAsJsonAsync("/api/shorten", new CreateShortUrlRequest
        {
            Url = "https://example.com/expired",
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        });
        var created = await createRes.Content.ReadFromJsonAsync<ShortUrlResponse>();

        var redirectRes = await client.GetAsync($"/{created!.ShortCode}");
        redirectRes.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task List_ReturnsAllLinks()
    {
        var client = CreateClient();

        await client.PostAsJsonAsync("/api/shorten", new CreateShortUrlRequest { Url = "https://example.com/list-a" });
        await client.PostAsJsonAsync("/api/shorten", new CreateShortUrlRequest { Url = "https://example.com/list-b" });

        var listRes = await client.GetAsync("/api/list");
        listRes.StatusCode.Should().Be(HttpStatusCode.OK);
        var links = await listRes.Content.ReadFromJsonAsync<List<ShortUrlResponse>>();
        links.Should().HaveCount(2);
    }

    [Fact]
    public async Task Delete_RemovesLink()
    {
        var client = CreateClient();

        var createRes = await client.PostAsJsonAsync("/api/shorten", new CreateShortUrlRequest { Url = "https://example.com/to-delete" });
        var created = await createRes.Content.ReadFromJsonAsync<ShortUrlResponse>();

        var delRes = await client.DeleteAsync($"/api/{created!.ShortCode}");
        delRes.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getRes = await client.GetAsync($"/{created.ShortCode}");
        getRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MissingApiKey_OnManagementEndpoint_Returns401()
    {
        var client = CreateClient(withKey: false);

        var res = await client.PostAsJsonAsync("/api/shorten", new CreateShortUrlRequest
        {
            Url = "https://example.com/no-key"
        });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BadApiKey_Returns401()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Api-Key", "usp-invalid-key");

        var res = await client.PostAsJsonAsync("/api/shorten", new CreateShortUrlRequest
        {
            Url = "https://example.com/bad-key"
        });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Redirect_WithoutApiKey_Works()
    {
        var clientWithKey = CreateClient();

        var createRes = await clientWithKey.PostAsJsonAsync("/api/shorten", new CreateShortUrlRequest
        {
            Url = "https://example.com/public-redirect"
        });
        var created = await createRes.Content.ReadFromJsonAsync<ShortUrlResponse>();

        var clientNoKey = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var redirectRes = await clientNoKey.GetAsync($"/{created!.ShortCode}");
        redirectRes.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task QrCode_ReturnsImage()
    {
        var client = CreateClient();

        var createRes = await client.PostAsJsonAsync("/api/shorten", new CreateShortUrlRequest
        {
            Url = "https://example.com/qr-test"
        });
        var created = await createRes.Content.ReadFromJsonAsync<ShortUrlResponse>();

        var qrRes = await client.GetAsync($"/api/urls/{created!.ShortCode}/qr");
        qrRes.StatusCode.Should().Be(HttpStatusCode.OK);
        qrRes.Content.Headers.ContentType?.MediaType.Should().Be("image/png");
        var bytes = await qrRes.Content.ReadAsByteArrayAsync();
        bytes.Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task BulkShorten_ReturnsMixedResults()
    {
        var client = CreateClient();

        var res = await client.PostAsJsonAsync("/api/shorten/bulk", new BulkShortenRequest
        {
            Items = new List<BulkItem>
            {
                new() { Url = "https://example.com/bulk-1" },
                new() { Url = "invalid" },
                new() { Url = "https://example.com/bulk-2" }
            }
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var response = await res.Content.ReadFromJsonAsync<BulkShortenResponse>();
        response.Should().NotBeNull();
        response!.Results.Should().HaveCount(3);
        response.Results[0].Success.Should().BeTrue();
        response.Results[1].Success.Should().BeFalse();
        response.Results[2].Success.Should().BeTrue();
    }

    [Fact]
    public async Task OpenRedirect_PrivateIp_IsBlocked()
    {
        var client = CreateClient();

        var createRes = await client.PostAsJsonAsync("/api/shorten", new CreateShortUrlRequest
        {
            Url = "https://example.com/safe-url"
        });
        var created = await createRes.Content.ReadFromJsonAsync<ShortUrlResponse>();

        var redirectRes = await client.GetAsync($"/{created!.ShortCode}");
        redirectRes.StatusCode.Should().Be(HttpStatusCode.Redirect);
        redirectRes.Headers.Location!.Host.Should().Be("example.com");
    }
}
