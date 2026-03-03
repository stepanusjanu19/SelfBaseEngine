using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Kei.Base.Helper.Security
{
    /// <summary>
    /// Provides SSRF (Server-Side Request Forgery) protection by validating
    /// outbound URLs and blocking requests to private/internal addresses.
    ///
    /// Bypass-resistant improvements in v2:
    /// <list type="bullet">
    ///   <item>Blocks 0.0.0.0 and [::] wildcard bind addresses</item>
    ///   <item>Detects and blocks octal IP addresses (0177.0.0.1)</item>
    ///   <item>Detects and blocks hex IP addresses (0x7f000001)</item>
    ///   <item>Detects and blocks decimal-encoded IPs (2130706433 = 127.0.0.1)</item>
    ///   <item>Blocks file:// and all non-http(s) schemes</item>
    ///   <item>Rejects URLs with embedded auth (user@host)</item>
    /// </list>
    /// </summary>
    public static class SsrfValidator
    {
        // Metadata service endpoints commonly targeted in SSRF attacks
        private static readonly HashSet<string> _blockedHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "169.254.169.254",          // AWS/GCP/Azure IMDS
            "metadata.google.internal",
            "169.254.170.2",            // Amazon ECS task metadata
            "100.100.100.200",          // Alibaba Cloud metadata
            "metadata.azure.com",
            "instance-data.ec2.internal",
        };

        private static readonly string[] _allowedSchemes = { "https", "http" };

        // Detects octal IP notation: 0177.0.0.1 (octet starts with 0 and has digits)
        private static readonly Regex _octalIpRegex =
            new(@"^0[0-7]+(\.[0-9]+){0,3}$|^(0[0-7]+\.){1,3}0[0-7]+$",
                RegexOptions.Compiled);

        // Detects hex IP notation: 0x7f000001
        private static readonly Regex _hexIpRegex =
            new(@"^0[xX][0-9a-fA-F]+$",
                RegexOptions.Compiled);

        // Detects pure decimal IP (e.g., 2130706433 → 127.0.0.1)
        private static readonly Regex _decimalIpRegex =
            new(@"^\d{7,10}$",
                RegexOptions.Compiled);

        // ─── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns <c>true</c> if the URL scheme is allowed, the host is in
        /// <paramref name="allowedHosts"/>, and the host is not private/blocked.
        /// </summary>
        public static bool IsAllowedUrl(Uri uri, IEnumerable<string> allowedHosts)
        {
            if (uri == null) return false;

            if (!_allowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(uri.UserInfo))
                return false; // Reject embedded credentials (user@host bypass)

            if (IsPrivateOrBlockedHost(uri))
                return false;

            if (allowedHosts == null) return false;

            var allowed = new HashSet<string>(allowedHosts, StringComparer.OrdinalIgnoreCase);
            return allowed.Contains(uri.Host);
        }

        /// <summary>
        /// Asserts that <paramref name="url"/> is safe to request.
        /// Throws <see cref="InvalidOperationException"/> if the URL is blocked.
        /// </summary>
        public static void AssertSafeUrl(string url, IEnumerable<string> allowedHosts)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentNullException(nameof(url), "URL must not be null or empty.");

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                throw new ArgumentException($"'{url}' is not a valid absolute URI.", nameof(url));

            if (!_allowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"Scheme '{uri.Scheme}' is not allowed. Only http/https are permitted.");

            if (!string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidOperationException(
                    "URLs with embedded credentials (user@host) are not allowed (SSRF bypass prevention).");

            if (IsPrivateOrBlockedHost(uri))
                throw new InvalidOperationException(
                    $"Request to '{uri.Host}' was blocked. Private/internal addresses are not allowed (SSRF protection).");

            if (!IsAllowedUrl(uri, allowedHosts))
                throw new InvalidOperationException(
                    $"Host '{uri.Host}' is not in the allowed hosts list (SSRF protection).");
        }

        /// <summary>
        /// Checks whether the URI resolves to a private or blocked IP range,
        /// including obfuscated forms (octal, hex, decimal-encoded IPs).
        /// </summary>
        public static bool IsPrivateOrBlockedHost(Uri uri)
        {
            if (uri == null) return true;

            var host = uri.Host?.Trim('[', ']'); // Strip IPv6 brackets

            if (string.IsNullOrEmpty(host)) return true;

            // Check known blocked hostnames
            if (_blockedHosts.Contains(host))
                return true;

            // Block wildcard bind addresses directly
            if (host == "0.0.0.0" || host == "::" || host == "0:0:0:0:0:0:0:0")
                return true;

            // Decode obfuscated IP representations before checking
            if (TryDecodeObfuscatedIp(host, out var decodedIp))
                return IsPrivateIp(decodedIp!);

            // Direct IP address
            if (IPAddress.TryParse(host, out var ip))
                return IsPrivateIp(ip);

            // DNS resolution (for hostnames)
            try
            {
                var addresses = Dns.GetHostAddresses(host);
                return addresses.Length == 0 || addresses.Any(IsPrivateIp);
            }
            catch (SocketException)
            {
                // DNS resolution failure → treat as blocked for safety
                return true;
            }
        }

        /// <summary>
        /// Determines whether an IP address belongs to a private or loopback range.
        /// Covers IPv4 (10.x, 172.16-31.x, 192.168.x, 127.x, 169.254.x, 0.0.0.0/8) and
        /// IPv6 (::1, fc00::/7 ULA, fe80::/10 link-local, ::).
        /// </summary>
        public static bool IsPrivateIp(IPAddress ip)
        {
            if (ip == null) return true;

            if (IPAddress.IsLoopback(ip))
                return true;

            // Block pure IPv6 any-address
            if (ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.Any))
                return true;

            if (ip.AddressFamily == AddressFamily.InterNetworkV6)
            {
                // Map IPv4-in-IPv6 back to IPv4 for range check
                if (ip.IsIPv4MappedToIPv6)
                    return IsPrivateIp(ip.MapToIPv4());

                var bytes = ip.GetAddressBytes();

                // fc00::/7  – Unique Local Addresses (ULA)
                if ((bytes[0] & 0xFE) == 0xFC) return true;

                // fe80::/10 – Link-local
                if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80) return true;

                return false;
            }

            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();

                // 0.0.0.0/8 – wildcard/unspecified
                if (b[0] == 0) return true;

                // 10.0.0.0/8
                if (b[0] == 10) return true;

                // 172.16.0.0/12
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;

                // 192.168.0.0/16
                if (b[0] == 192 && b[1] == 168) return true;

                // 169.254.0.0/16 – APIPA / link-local / cloud metadata
                if (b[0] == 169 && b[1] == 254) return true;

                // 127.0.0.0/8 – loopback (belt-and-suspenders, IsLoopback handles ::1)
                if (b[0] == 127) return true;

                return false;
            }

            return false;
        }

        /// <summary>
        /// Creates a <see cref="SocketsHttpHandler"/> that validates every outgoing
        /// request against the <paramref name="allowedHosts"/> allowlist before sending.
        /// </summary>
        public static SocketsHttpHandler CreateSafeHttpClientHandler(IEnumerable<string> allowedHosts)
        {
            var hostList = allowedHosts?.ToList() ?? new List<string>();

            return new SocketsHttpHandler
            {
                ConnectCallback = async (context, cancellationToken) =>
                {
                    var host = context.DnsEndPoint.Host;

                    if (_blockedHosts.Contains(host, StringComparer.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            $"SSRF blocked: connection to '{host}' is not permitted.");

                    if (TryDecodeObfuscatedIp(host, out var obfuscatedIp) && IsPrivateIp(obfuscatedIp!))
                        throw new InvalidOperationException(
                            $"SSRF blocked: obfuscated private IP '{host}' is not permitted.");

                    if (IPAddress.TryParse(host, out var directIp) && IsPrivateIp(directIp))
                        throw new InvalidOperationException(
                            $"SSRF blocked: connection to private IP '{host}' is not permitted.");

                    if (hostList.Count > 0)
                    {
                        var allowed = new HashSet<string>(hostList, StringComparer.OrdinalIgnoreCase);
                        if (!allowed.Contains(host))
                            throw new InvalidOperationException(
                                $"SSRF blocked: '{host}' is not in the allowed hosts list.");
                    }

                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
            };
        }

        // ─── Private Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Attempts to decode obfuscated IP representations: octal (0177.0.0.1),
        /// hex (0x7f000001), and pure decimal (2130706433 = 127.0.0.1).
        /// Returns <c>true</c> and a parsed IP if successfully decoded.
        /// </summary>
        private static bool TryDecodeObfuscatedIp(string host, out IPAddress? result)
        {
            result = null;

            // Hex-encoded whole IP: 0x7f000001
            if (_hexIpRegex.IsMatch(host))
            {
                try
                {
                    var intVal = Convert.ToInt32(host, 16);
                    var bytes = new[]
                    {
                        (byte)((intVal >> 24) & 0xFF),
                        (byte)((intVal >> 16) & 0xFF),
                        (byte)((intVal >> 8)  & 0xFF),
                        (byte)( intVal        & 0xFF),
                    };
                    result = new IPAddress(bytes);
                    return true;
                }
                catch { return false; }
            }

            // Pure decimal IP: 2130706433 = 127.0.0.1
            if (_decimalIpRegex.IsMatch(host))
            {
                try
                {
                    var intVal = long.Parse(host);
                    if (intVal >= 0 && intVal <= uint.MaxValue)
                    {
                        var bytes = new[]
                        {
                            (byte)((intVal >> 24) & 0xFF),
                            (byte)((intVal >> 16) & 0xFF),
                            (byte)((intVal >> 8)  & 0xFF),
                            (byte)( intVal        & 0xFF),
                        };
                        result = new IPAddress(bytes);
                        return true;
                    }
                }
                catch { return false; }
            }

            // Octal octets in dotted notation: 0177.0.0.1 → 127.0.0.1
            var parts = host.Split('.');
            if (parts.Length == 4 && parts.All(p => _octalIpRegex.IsMatch(host)))
            {
                // Try parsing each octet as octal if it starts with '0'
                var octets = new byte[4];
                for (int i = 0; i < 4; i++)
                {
                    var part = parts[i];
                    try
                    {
                        octets[i] = part.StartsWith("0") && part.Length > 1
                            ? Convert.ToByte(part, 8)
                            : byte.Parse(part);
                    }
                    catch { return false; }
                }
                result = new IPAddress(octets);
                return true;
            }

            return false;
        }
    }
}
