# URL Shortener Pro

A secure, feature-rich URL shortening service built with ASP.NET Core 9, Entity Framework Core, and SQLite.

## Features

- **Short URL Creation** — Generate short, shareable links with Base62 or cryptographic 7-character codes
- **Custom Aliases** — Optional user-defined aliases (3-30 chars, alphanumeric + hyphens)
- **Expiration / TTL** — Optional link expiration; expired links return HTTP 410 Gone
- **Click Analytics** — Per-link stats with daily click buckets and top referrers
- **QR Code Generation** — Server-side QR code PNGs via QRCoder
- **Bulk Shortening** — Create many short URLs in a single POST
- **API Key Authentication** — Hash-based API key auth on management endpoints; redirect endpoint is public
- **Rate Limiting** — 20 creates/minute per IP on shorten endpoints
- **SSRF / Open Redirect Protection** — Validates URLs are well-formed, http/https-only, and not private/loopback addresses
- **Dual Theme UI** — Light/dark mode with persistent toggle, responsive down to 360px
- **Swagger Docs** — Full OpenAPI documentation with XML comments

## Architecture

```
ShortnerUrl/
├── Controllers/        # Thin controllers delegating to service layer
│   ├── UrlController.cs
│   ├── RedirectController.cs
│   └── ApiKeyController.cs
├── Services/           # Business logic layer
│   ├── IUrlShortenerService.cs
│   ├── UrlShortenerService.cs
│   ├── CodeGenerator.cs
│   └── UrlValidator.cs
├── Dtos/               # Request/response DTOs (never expose entities)
├── Models/             # EF Core entities
├── Data/               # DbContext with Fluent API config
├── Middleware/         # Exception handling, API key auth
├── Validators/         # FluentValidation validators
├── Migrations/         # EF Core migrations
└── wwwroot/            # SPA frontend (vanilla JS + Chart.js)
```

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQLite (auto-managed via EF Core)

## Setup

```bash
git clone <repo-url>
cd ShortnerUrl
dotnet restore
dotnet ef database update
dotnet run
```

The app starts at `https://localhost:5001` (or `http://localhost:5000`).

## Getting an API Key

All management endpoints require an `X-Api-Key` header. Generate a key:

```bash
curl -X POST https://localhost:5001/api/keys/create \
  -H "Content-Type: application/json" \
  -d '{"workspaceName": "my-workspace"}'
```

Store the returned `plainTextKey` — it won't be shown again.

## API Reference

### Create Short URL

```http
POST /api/shorten
Content-Type: application/json
X-Api-Key: usp_<your-key>

{
  "url": "https://example.com/very-long-path",
  "customAlias": "my-link",
  "expiresAt": "2027-01-01T00:00:00Z"
}
```

Response: `200 OK`
```json
{
  "id": 1,
  "longUrl": "https://example.com/very-long-path",
  "shortUrl": "http://localhost:5000/my-link",
  "shortCode": "my-link",
  "createdAt": "2026-07-27T20:15:00Z",
  "clicks": 0,
  "expiresAt": "2027-01-01T00:00:00Z",
  "customAlias": "my-link",
  "isExpired": false
}
```

### Redirect (Public — No API Key Needed)

```http
GET /{shortCode}
```
- Returns `302` with `Location` header to the original URL
- Returns `404` if code not found
- Returns `410` if link is expired

### List All Links

```http
GET /api/list
X-Api-Key: usp_<your-key>
```

### Get Stats

```http
GET /api/urls/{code}/stats
X-Api-Key: usp_<your-key>
```

### Get QR Code

```http
GET /api/urls/{code}/qr
```
Returns PNG image.

### Delete Link

```http
DELETE /api/{code}
X-Api-Key: usp_<your-key>
```

### Bulk Shorten

```http
POST /api/shorten/bulk
Content-Type: application/json
X-Api-Key: usp_<your-key>

{
  "items": [
    { "url": "https://example.com/1" },
    { "url": "https://example.com/2", "customAlias": "second" }
  ]
}
```

## Running Tests

```bash
# Unit + integration tests
dotnet test

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Security

- **API Key Hashing** — Keys are stored as SHA-256 hashes; plaintext never persisted
- **SSRF Protection** — URL validation rejects private/loopback IPs (127.0.0.1, 10.x, 192.168.x, 169.254.x, localhost)
- **Open Redirect Prevention** — Redirect validates stored URL before following
- **Rate Limiting** — 20 create requests/minute per IP
- **Input Validation** — FluentValidation on all public endpoints
- **HTTPS** — Enabled by default with HSTS

## Tech Stack

| Component | Library |
|-----------|---------|
| Framework | ASP.NET Core 9 |
| ORM | EF Core 9 + SQLite |
| Validation | FluentValidation |
| QR Codes | QRCoder |
| Docs | Swagger / Swashbuckle |
| Testing | xUnit + Moq + FluentAssertions |
| CI | GitHub Actions |
| Frontend | Vanilla JS + Chart.js |
