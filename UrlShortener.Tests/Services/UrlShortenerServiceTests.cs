using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using ShortnerUrl.Data;
using ShortnerUrl.Dtos;
using ShortnerUrl.Exceptions;
using ShortnerUrl.Services;

namespace UrlShortener.Tests.Services;

public class UrlShortenerServiceTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private UrlShortenerService CreateService(AppDbContext ctx)
    {
        var logger = new Mock<ILogger<UrlShortenerService>>().Object;
        return new UrlShortenerService(ctx, logger);
    }

    [Fact]
    public async Task CreateShortUrlAsync_WithValidUrl_ReturnsResponse()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var request = new CreateShortUrlRequest { Url = "https://example.com/very-long-path" };

        var result = await svc.CreateShortUrlAsync(request, "http://localhost:5000");

        result.Should().NotBeNull();
        result.ShortCode.Should().NotBeNullOrEmpty();
        result.ShortCode.Length.Should().Be(7);
        result.ShortUrl.Should().Be($"http://localhost:5000/{result.ShortCode}");
        result.LongUrl.Should().Be("https://example.com/very-long-path");
        result.Clicks.Should().Be(0);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateShortUrlAsync_InvalidUrl_Throws()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var request = new CreateShortUrlRequest { Url = "not-a-url" };

        await FluentActions.Awaiting(() => svc.CreateShortUrlAsync(request, "http://localhost:5000"))
            .Should().ThrowAsync<ValidationException>()
            .WithMessage("URL must be an absolute URI*");
    }

    [Fact]
    public async Task CreateShortUrlAsync_PrivateIpUrl_Throws()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var testUrls = new[]
        {
            "http://127.0.0.1/some-path",
            "http://localhost/secret",
            "http://10.0.0.1/admin",
            "http://192.168.1.1/config",
            "http://169.254.1.1/autoconfig"
        };

        foreach (var url in testUrls)
        {
            var request = new CreateShortUrlRequest { Url = url };
            await FluentActions.Awaiting(() => svc.CreateShortUrlAsync(request, "http://localhost:5000"))
                .Should().ThrowAsync<ValidationException>()
                .WithMessage("*private*");
        }
    }

    [Fact]
    public async Task CreateShortUrlAsync_WithCustomAlias_ReturnsAlias()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var request = new CreateShortUrlRequest
        {
            Url = "https://example.com",
            CustomAlias = "my-link"
        };

        var result = await svc.CreateShortUrlAsync(request, "http://localhost:5000");

        result.ShortCode.Should().Be("my-link");
        result.ShortUrl.Should().Be("http://localhost:5000/my-link");
    }

    [Fact]
    public async Task CreateShortUrlAsync_ReservedAlias_Throws()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var reserved = new[] { "api", "admin", "static", "swagger" };
        foreach (var alias in reserved)
        {
            var request = new CreateShortUrlRequest
            {
                Url = "https://example.com",
                CustomAlias = alias
            };
            await FluentActions.Awaiting(() => svc.CreateShortUrlAsync(request, "http://localhost:5000"))
                .Should().ThrowAsync<ValidationException>()
                .WithMessage("*reserved*");
        }
    }

    [Fact]
    public async Task CreateShortUrlAsync_DuplicateAlias_Throws()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var r1 = new CreateShortUrlRequest { Url = "https://example.com/a", CustomAlias = "taken" };
        var r2 = new CreateShortUrlRequest { Url = "https://example.com/b", CustomAlias = "taken" };

        await svc.CreateShortUrlAsync(r1, "http://localhost:5000");
        await FluentActions.Awaiting(() => svc.CreateShortUrlAsync(r2, "http://localhost:5000"))
            .Should().ThrowAsync<ConflictException>()
            .WithMessage("*already taken*");
    }

    [Fact]
    public async Task RedirectAsync_ExistingCode_ReturnsUrlAndIncrementsClick()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var request = new CreateShortUrlRequest { Url = "https://example.com" };
        var created = await svc.CreateShortUrlAsync(request, "http://localhost:5000");

        var result = await svc.RedirectAsync(created.ShortCode, null, null, null);

        result.Should().Be("https://example.com");

        var stats = await svc.GetStatsAsync(created.ShortCode);
        stats.Should().NotBeNull();
        stats!.TotalClicks.Should().Be(1);
    }

    [Fact]
    public async Task RedirectAsync_NonExistentCode_ReturnsNull()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.RedirectAsync("nonexist", null, null, null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RedirectAsync_ExpiredLink_ReturnsExpired()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        // Create with a future date, then manually set it to the past to simulate expiration
        var request = new CreateShortUrlRequest
        {
            Url = "https://example.com",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        var created = await svc.CreateShortUrlAsync(request, "http://localhost:5000");

        // Manually expire the link
        var entity = await ctx.UrlShorteners.FirstAsync(x => x.ShortCode == created.ShortCode);
        entity.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await ctx.SaveChangesAsync();

        var result = await svc.RedirectAsync(created.ShortCode, null, null, null);

        result.Should().Be("EXPIRED");
    }

    [Fact]
    public async Task RedirectAsync_InactiveLink_ReturnsExpired()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var request = new CreateShortUrlRequest { Url = "https://example.com" };
        var created = await svc.CreateShortUrlAsync(request, "http://localhost:5000");

        // Deactivate the link
        var entity = await ctx.UrlShorteners.FirstAsync(x => x.ShortCode == created.ShortCode);
        entity.IsActive = false;
        await ctx.SaveChangesAsync();

        var result = await svc.RedirectAsync(created.ShortCode, null, null, null);

        result.Should().Be("EXPIRED");
    }

    [Fact]
    public async Task DeleteAsync_ExistingCode_ReturnsTrue()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var request = new CreateShortUrlRequest { Url = "https://example.com" };
        var created = await svc.CreateShortUrlAsync(request, "http://localhost:5000");

        var deleted = await svc.DeleteAsync(created.ShortCode);
        deleted.Should().BeTrue();

        var result = await svc.RedirectAsync(created.ShortCode, null, null, null);
        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentCode_ReturnsFalse()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var deleted = await svc.DeleteAsync("nonexist");
        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetStatsAsync_PopulatesClickData()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var request = new CreateShortUrlRequest { Url = "https://example.com" };
        var created = await svc.CreateShortUrlAsync(request, "http://localhost:5000");

        await svc.RedirectAsync(created.ShortCode, "https://referrer.com", "TestAgent/1.0", "1.2.3.4");
        await svc.RedirectAsync(created.ShortCode, "https://referrer.com", "TestAgent/2.0", "5.6.7.8");
        await svc.RedirectAsync(created.ShortCode, "https://other.com", null, null);

        var stats = await svc.GetStatsAsync(created.ShortCode);
        stats.Should().NotBeNull();
        stats!.TotalClicks.Should().Be(3);
        stats.TopReferrers.Should().HaveCount(2);
        stats.TopReferrers[0].Count.Should().Be(2);
        stats.TopReferrers[0].Referrer.Should().Be("https://referrer.com");
        stats.ClicksOverTime.Should().HaveCount(1);
        stats.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task BulkShortenAsync_MixedResults_ReturnsSuccessAndFailures()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var request = new BulkShortenRequest
        {
            Items = new List<BulkItem>
            {
                new() { Url = "https://example.com/1" },
                new() { Url = "invalid-url" },
                new() { Url = "https://example.com/2" }
            }
        };

        var results = await svc.BulkShortenAsync(request, "http://localhost:5000");

        results.Should().HaveCount(3);
        results[0].Success.Should().BeTrue();
        results[1].Success.Should().BeFalse();
        results[2].Success.Should().BeTrue();
    }

    [Fact]
    public async Task CreateApiKeyAsync_ReturnsKeyAndPersistsHash()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var plainKey = await svc.CreateApiKeyAsync("test-workspace");

        plainKey.Should().StartWith("usp_");
        plainKey.Length.Should().BeGreaterThan(40);

        var hash = UrlShortenerService.HashKey(plainKey);
        var stored = await ctx.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash);
        stored.Should().NotBeNull();
        stored!.WorkspaceName.Should().Be("test-workspace");
        stored.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetQrCodeAsync_ReturnsQrBytes()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var request = new CreateShortUrlRequest { Url = "https://example.com" };
        var created = await svc.CreateShortUrlAsync(request, "http://localhost:5000");

        var qrBytes = await svc.GetQrCodeAsync(created.ShortCode, "http://localhost:5000");

        qrBytes.Should().NotBeNull();
        qrBytes!.Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task GetQrCodeAsync_NonExistent_ReturnsNull()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var qrBytes = await svc.GetQrCodeAsync("nonexist", "http://localhost:5000");

        qrBytes.Should().BeNull();
    }

    [Fact]
    public async Task GenerateQrCode_WithCustomColors_ReturnsBytes()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var qrBytes = svc.GenerateQrCode("https://example.com", 15, "#FF0000", "#FFFFFF");

        qrBytes.Should().NotBeNull();
        qrBytes.Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task CreateShortUrlAsync_ReturnsExistingForSameUrl()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var request = new CreateShortUrlRequest { Url = "https://example.com/same-url" };

        var first = await svc.CreateShortUrlAsync(request, "http://localhost:5000");
        var second = await svc.CreateShortUrlAsync(request, "http://localhost:5000");

        second.ShortCode.Should().Be(first.ShortCode);
    }

    [Fact]
    public async Task ListAllAsync_ReturnsAllUrls()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        await svc.CreateShortUrlAsync(new CreateShortUrlRequest { Url = "https://example.com/a" }, "http://localhost:5000");
        await svc.CreateShortUrlAsync(new CreateShortUrlRequest { Url = "https://example.com/b" }, "http://localhost:5000");

        var list = await svc.ListAllAsync("http://localhost:5000");

        list.Should().HaveCount(2);
        list[0].LongUrl.Should().Be("https://example.com/b");
    }

    [Fact]
    public async Task UpdateUrlAsync_ChangesDestination()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var created = await svc.CreateShortUrlAsync(
            new CreateShortUrlRequest { Url = "https://example.com/old" }, "http://localhost:5000");

        var updated = await svc.UpdateUrlAsync(created.ShortCode,
            new UpdateUrlRequest { Url = "https://example.com/new" }, "http://localhost:5000");

        updated.Should().NotBeNull();
        updated!.LongUrl.Should().Be("https://example.com/new");
    }

    [Fact]
    public async Task UpdateUrlAsync_Deactivate()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);
        var created = await svc.CreateShortUrlAsync(
            new CreateShortUrlRequest { Url = "https://example.com" }, "http://localhost:5000");

        var updated = await svc.UpdateUrlAsync(created.ShortCode,
            new UpdateUrlRequest { IsActive = false }, "http://localhost:5000");

        updated.Should().NotBeNull();
        updated!.IsActive.Should().BeFalse();

        var redirect = await svc.RedirectAsync(created.ShortCode, null, null, null);
        redirect.Should().Be("EXPIRED");
    }

    [Fact]
    public async Task UpdateUrlAsync_NonExistent_ReturnsNull()
    {
        using var ctx = CreateDbContext();
        var svc = CreateService(ctx);

        var result = await svc.UpdateUrlAsync("nonexist",
            new UpdateUrlRequest { Url = "https://example.com" }, "http://localhost:5000");

        result.Should().BeNull();
    }
}
