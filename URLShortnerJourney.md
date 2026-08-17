# URL Shortener Pro — Project Journey & Tracking

Reference doc for me (the AI) and the user. Keep updated after every session. This file is the single source of truth for what's done, what's broken, and what's next.

---

## 1. Project Overview

- **Name:** Shortener Pro (URL Shortener)
- **Purpose:** Secure, feature-rich URL shortening service with dashboard UI
- **Solution:** `ShortnerUrl.sln` (repo root)
- **Stack:**
  - ASP.NET Core (net9.0), MVC controllers + static SPA-style frontend
  - EF Core 9 + SQLite (`urls.db` in `ShortnerUrl/`)
  - FluentValidation, Swagger, QRCoder, Chart.js (frontend)
  - xUnit test suite (`UrlShortener.Tests`), GitHub Actions CI (`.github/workflows/ci.yml`)
- **Branch:** `master`

## 2. How to Build / Run / Test

```powershell
# Build
dotnet build ShortnerUrl.sln

# Run (dev)
dotnet run --project ShortnerUrl            # http profile -> http://localhost:5122
# NOTE: https profile also works (https://localhost:7179)

# Tests
dotnet test ShortnerUrl.sln

# Default dev API key: printed to console at startup (usp_dev_...). Or create new:
# POST /api/keys/create  { "workspaceName": "..." }
```

## 3. Architecture / File Map

| Path | Role |
|---|---|
| `ShortnerUrl/Program.cs` | Startup: DbContext(SQLite), DI, Swagger, rate limiter, middleware pipeline, DB migrate + dev key seed |
| `ShortnerUrl/Controllers/UrlController.cs` | API: `/api/shorten`, `/api/list`, `DELETE /api/{code}`, `/api/urls/{code}/stats`, `/api/urls/{code}/qr`, `/api/shorten/bulk` |
| `ShortnerUrl/Controllers/RedirectController.cs` | `GET /{code}` -> 302 redirect, records click, handles EXPIRED (410) |
| `ShortnerUrl/Controllers/ApiKeyController.cs` | `POST /api/keys/create` -> generates + hashes API key |
| `ShortnerUrl/Services/UrlShortenerService.cs` | Core business logic (create/redirect/list/delete/stats/qr/bulk/keys) |
| `ShortnerUrl/Services/UrlValidator.cs` | URL + alias validation, SSRF/private-IP block |
| `ShortnerUrl/Services/CodeGenerator.cs` | Cryptographic 7-char codes + base62 |
| `ShortnerUrl/Middleware/ApiKeyAuthMiddleware.cs` | X-Api-Key check for `/api/*` (public: `/api/health`) |
| `ShortnerUrl/Middleware/ExceptionHandlingMiddleware.cs` | Global exception -> JSON (400/409/500) |
| `ShortnerUrl/Data/AppDbContext.cs` | EF context: UrlShorteners (unique ShortCode), UrlClicks, ApiKeys (unique KeyHash) |
| `ShortnerUrl/Models/` | `UrlShortener`, `UrlClick`, `ApiKey` |
| `ShortnerUrl/Dtos/` | Requests/responses for all endpoints |
| `ShortnerUrl/Validators/` | FluentValidation for CreateShortUrlRequest |
| `ShortnerUrl/wwwroot/` | Frontend: `index.html`, `js/app.js`, `css/app.css` |
| `UrlShortener.Tests/Integration/ApiTests.cs` | 13 integration tests (WebApplicationFactory) |
| `UrlShortener.Tests/Services/UrlShortenerServiceTests.cs` | Service unit tests |

## 4. API Reference (quick)

| Method | Route | Auth | Notes |
|---|---|---|---|
| POST | `/api/shorten` | X-Api-Key | body: `{ url, customAlias?, expiresAt? }` |
| GET | `/api/list` | X-Api-Key | all links, newest first |
| DELETE | `/api/{code}` | X-Api-Key | 204 |
| GET | `/api/urls/{code}/stats` | X-Api-Key | clicks over 30d, top referrers |
| GET | `/api/urls/{code}/qr` | X-Api-Key | PNG image |
| POST | `/api/shorten/bulk` | X-Api-Key | mixed success/fail results |
| POST | `/api/keys/create` | NONE (intended) | returns plaintext key ONCE |
| GET | `/{code}` | NONE | 302 redirect (public) |

## 5. Findings & Bugs (status tracker)

| # | Issue | Severity | Status |
|---|---|---|---|
| 1 | `ApiKeyAuthMiddleware` declares `PublicPaths` (incl. `/api/keys/create`) but NEVER uses it -> `POST /api/keys/create` returns **401** -> frontend `ensureApiKey()` fails silently -> **ALL UI requests 401, dashboard completely broken** | CRITICAL | **FIXED** (2026-08-17): added `PublicPaths.Contains(path)` to bypass logic. Verified: create key = 200 |
| 2 | `UseHttpsRedirection()` with `http` launch profile redirects to `https://localhost:443` (nothing listening) -> UI breaks when launched via http profile | HIGH | **NOT AN ISSUE** - verified app serves `/` with 200 over http (middleware degrades gracefully when no https port configured) |
| 3 | Rate limiter policy `CreateRateLimit` registered but never applied (`[EnableRateLimiting]` missing on controllers) -> no 429 ever | LOW | **FIXED** (2026-08-17): added `[EnableRateLimiting("CreateRateLimit")]` to `POST /api/shorten` + `/api/shorten/bulk` |
| 4 | `ShortnerUrl.http` references non-existent `/weatherforecast` endpoint (stale template file) | LOW | **FIXED** (2026-08-17): rewrote with real endpoints + API key placeholder |
| 5 | Duplicate `Url` returns existing short URL even if it is EXPIRED | LOW | PENDING |
| 6 | | | |

## 6. Security & Design Notes (already implemented)

- API keys hashed with SHA-256 (plaintext shown once at creation)
- SSRF protection: blocks private/loopback/link-local IPs (10/8, 127/8, 169.254/16, 192.168/16, 172.16/12)
- Reserved aliases blocked (api, admin, swagger, etc.)
- ShortCode unique index + collision retry (5 attempts), crypto RNG
- Redirect validates target is absolute http/https before redirecting
- Frontend XSS escaped in table rendering (`escapeHtml`)

## 7. Progress Log

- **2026-08-17** — Session start: full codebase audit. Built successfully (0 warnings/errors). Confirmed CRITICAL bug #1 (API key bootstrap broken -> whole UI dead). Verified app serves `/` with 200. **Fixed #1, #3, #4.** Full end-to-end verification PASSED (create key 200, shorten 200, redirect 302, stats 200 with click count, QR image/png, list 200, delete 204, frontend 200). App is live on `http://localhost:5122`. Next: fix #5, run `dotnet test`, frontend UX audit.

## 8. Conventions / Gotchas

- Project dir is `ShortnerUrl` (not `UrlShortener`) — namespace `ShortnerUrl.*`
- `InternalVisibleTo` on csproj for tests (HashKey is `internal static`)
- Frontend stores API key in cookie `apiKey` (plaintext in cookie — acceptable for dev)
- DB file `urls.db` is inside project; migrations auto-apply at startup
- Dev API key is logged at startup (WARNING level) — reset by deleting row in DB
- Lint/typecheck = `dotnet build`; tests = `dotnet test`
