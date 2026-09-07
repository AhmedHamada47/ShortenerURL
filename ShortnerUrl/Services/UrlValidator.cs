using System.Net;
using System.Text.RegularExpressions;

namespace ShortnerUrl.Services
{
    public static class UrlValidator
    {
        private static readonly Regex CustomAliasRegex = new("^[a-zA-Z0-9-]{3,30}$", RegexOptions.Compiled);

        private static readonly HashSet<string> ReservedAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            "api", "admin", "static", "swagger", "login", "register", "account",
            "dashboard", "qr", "stats", "list", "shorten", "bulk", "keys",
            "health", "favicon", "robots", "sitemap", "css", "js", "lib",
            "fonts", "images", "assets", "wwwroot"
        };

        private static readonly uint[] PrivateIpPrefixes =
        {
            0x0A000000, // 10.0.0.0/8
            0x7F000000, // 127.0.0.0/8
            0xA9FE0000, // 169.254.0.0/16
            0xC0A80000, // 192.168.0.0/16
            0xAC100000, // 172.16.0.0/12
        };

        private static readonly uint[] PrivateIpMasks =
        {
            0xFF000000, // /8
            0xFF000000, // /8
            0xFFFF0000, // /16
            0xFFFF0000, // /16
            0xFFF00000, // /12
        };

        public static (bool IsValid, string? Error) ValidateUrl(string url, int maxLength = 2048)
        {
            if (string.IsNullOrWhiteSpace(url))
                return (false, "URL is required.");

            if (url.Length > maxLength)
                return (false, $"URL must not exceed {maxLength} characters.");

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return (false, "URL must be an absolute URI (e.g. https://example.com).");

            if (uri.Scheme != "http" && uri.Scheme != "https")
                return (false, "Only http and https schemes are allowed.");

            if (IsPrivateHost(uri))
                return (false, "URL points to a private or loopback address, which is not allowed.");

            return (true, null);
        }

        public static (bool IsValid, string? Error) ValidateCustomAlias(string? alias)
        {
            if (string.IsNullOrWhiteSpace(alias))
                return (true, null);

            if (alias.Length < 3 || alias.Length > 30)
                return (false, "Custom alias must be between 3 and 30 characters.");

            if (!CustomAliasRegex.IsMatch(alias))
                return (false, "Custom alias may only contain letters, digits, and hyphens.");

            if (ReservedAliases.Contains(alias))
                return (false, "This alias is reserved and cannot be used.");

            return (true, null);
        }

        private static bool IsPrivateHost(Uri uri)
        {
            try
            {
                var host = uri.DnsSafeHost;

                if (host == "localhost")
                    return true;

                if (IPAddress.TryParse(host, out var parsedAddr))
                    return IsPrivateAddress(parsedAddr);

                // Resolve DNS to catch rebinding attacks (e.g. localtest.me -> 127.0.0.1)
                try
                {
                    var addresses = Dns.GetHostAddresses(host);
                    foreach (var addr in addresses)
                    {
                        if (IsPrivateAddress(addr))
                            return true;
                    }
                }
                catch
                {
                    // DNS resolution failed — treat as private (fail-closed)
                    return true;
                }

                return false;
            }
            catch
            {
                // Parsing error — assume private for safety
                return true;
            }
        }

        private static bool IsPrivateAddress(IPAddress addr)
        {
            if (IPAddress.IsLoopback(addr))
                return true;

            var bytes = addr.GetAddressBytes();

            if (bytes.Length == 4)
            {
                var ipInt = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
                for (int i = 0; i < PrivateIpPrefixes.Length; i++)
                {
                    if ((ipInt & PrivateIpMasks[i]) == PrivateIpPrefixes[i])
                        return true;
                }
            }
            else if (bytes.Length == 16)
            {
                // IPv6 private ranges: ::1 (loopback already caught), fe80::/10 (link-local), fc00::/7 (ULA)
                if (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80)
                    return true;
                if ((bytes[0] & 0xfe) == 0xfc)
                    return true;
            }

            return false;
        }
    }
}
