using System;
using System.Net;
using Kei.Base.Helper.Security;

namespace Kei.Base.Tests.Security
{
    /// <summary>
    /// Unit tests for SsrfValidator — covers private IP ranges, obfuscated IPs,
    /// blocked metadata hosts, scheme restrictions, and edge cases.
    /// </summary>
    public class SsrfValidatorTests
    {
        private static readonly string[] _allowed = { "api.example.com", "safe-partner.net" };

        // ─── IsPrivateIp ──────────────────────────────────────────────────────────

        [Theory]
        [InlineData("127.0.0.1")]       // loopback
        [InlineData("10.0.0.1")]        // RFC 1918
        [InlineData("10.255.255.255")]
        [InlineData("172.16.0.1")]      // RFC 1918 /12
        [InlineData("172.31.255.255")]
        [InlineData("192.168.1.1")]     // RFC 1918 /16
        [InlineData("169.254.169.254")] // APIPA / metadata
        [InlineData("0.0.0.1")]         // 0.0.0.0/8 unspecified
        public void IsPrivateIp_PrivateAddresses_ReturnsTrue(string ip)
        {
            Assert.True(SsrfValidator.IsPrivateIp(IPAddress.Parse(ip)));
        }

        [Theory]
        [InlineData("8.8.8.8")]
        [InlineData("1.1.1.1")]
        [InlineData("93.184.216.34")]  // example.com
        public void IsPrivateIp_PublicAddresses_ReturnsFalse(string ip)
        {
            Assert.False(SsrfValidator.IsPrivateIp(IPAddress.Parse(ip)));
        }

        [Fact]
        public void IsPrivateIp_Ipv6Loopback_ReturnsTrue()
        {
            Assert.True(SsrfValidator.IsPrivateIp(IPAddress.IPv6Loopback));
        }

        [Fact]
        public void IsPrivateIp_Ipv6ULA_ReturnsTrue()
        {
            // fc00::/7
            Assert.True(SsrfValidator.IsPrivateIp(IPAddress.Parse("fd12:3456:789a::1")));
        }

        [Fact]
        public void IsPrivateIp_Ipv6LinkLocal_ReturnsTrue()
        {
            // fe80::/10
            Assert.True(SsrfValidator.IsPrivateIp(IPAddress.Parse("fe80::1")));
        }

        // ─── AssertSafeUrl: Allowed URLs ─────────────────────────────────────────

        [Theory]
        [InlineData("https://8.8.8.8/api", "8.8.8.8")]
        [InlineData("http://8.8.4.4/path?q=1", "8.8.4.4")]
        public void AssertSafeUrl_AllowedIpUrl_DoesNotThrow(string url, string allowedHost)
        {
            // Use direct IPs to avoid DNS resolution dependency in test environment
            var allowedHosts = new[] { allowedHost };
            var ex = Record.Exception(() => SsrfValidator.AssertSafeUrl(url, allowedHosts));
            Assert.Null(ex);
        }

        // ─── AssertSafeUrl: Blocked Private Hosts ────────────────────────────────

        [Theory]
        [InlineData("http://127.0.0.1/admin")]
        [InlineData("http://10.0.0.1/secret")]
        [InlineData("http://192.168.1.1/config")]
        [InlineData("http://169.254.169.254/latest/meta-data/")]
        public void AssertSafeUrl_PrivateHost_Throws(string url)
        {
            Assert.Throws<InvalidOperationException>(() =>
                SsrfValidator.AssertSafeUrl(url, _allowed));
        }

        // ─── AssertSafeUrl: Cloud Metadata Endpoints ─────────────────────────────

        [Fact]
        public void AssertSafeUrl_AwsMetadata_Blocked()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SsrfValidator.AssertSafeUrl("http://169.254.169.254/latest/meta-data/", _allowed));
        }

        [Fact]
        public void AssertSafeUrl_GcpMetadata_Blocked()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SsrfValidator.AssertSafeUrl("http://metadata.google.internal/computeMetadata/v1/", _allowed));
        }

        // ─── AssertSafeUrl: Scheme Restrictions ─────────────────────────────────

        [Theory]
        [InlineData("file:///etc/passwd")]
        [InlineData("ftp://api.example.com/file")]
        [InlineData("ldap://api.example.com/")]
        [InlineData("gopher://api.example.com/")]
        public void AssertSafeUrl_DisallowedScheme_Throws(string url)
        {
            Assert.Throws<InvalidOperationException>(() =>
                SsrfValidator.AssertSafeUrl(url, _allowed));
        }

        // ─── AssertSafeUrl: Host Not in Allowlist ────────────────────────────────

        [Fact]
        public void AssertSafeUrl_NotAllowedHost_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SsrfValidator.AssertSafeUrl("https://evil.com/steal-data", _allowed));
        }

        // ─── AssertSafeUrl: Embedded Credentials Bypass ──────────────────────────

        [Fact]
        public void AssertSafeUrl_EmbeddedCredentials_Throws()
        {
            // user@host bypass attempt
            Assert.Throws<InvalidOperationException>(() =>
                SsrfValidator.AssertSafeUrl("http://user@169.254.169.254/", _allowed));
        }

        // ─── AssertSafeUrl: Obfuscated IP Bypasses ───────────────────────────────

        [Fact]
        public void AssertSafeUrl_HexEncodedIp_Blocked()
        {
            // 0x7f000001 = 127.0.0.1
            Assert.Throws<InvalidOperationException>(() =>
                SsrfValidator.AssertSafeUrl("http://0x7f000001/", _allowed));
        }

        [Fact]
        public void AssertSafeUrl_DecimalIp_Blocked()
        {
            // 2130706433 = 127.0.0.1
            Assert.Throws<InvalidOperationException>(() =>
                SsrfValidator.AssertSafeUrl("http://2130706433/", _allowed));
        }

        [Fact]
        public void AssertSafeUrl_WildcardIp_Blocked()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SsrfValidator.AssertSafeUrl("http://0.0.0.0/", _allowed));
        }

        // ─── AssertSafeUrl: Input Validation ─────────────────────────────────────

        [Fact]
        public void AssertSafeUrl_EmptyUrl_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SsrfValidator.AssertSafeUrl("", _allowed));
        }

        [Fact]
        public void AssertSafeUrl_InvalidUri_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                SsrfValidator.AssertSafeUrl("not-a-url", _allowed));
        }

        // ─── IsPrivateOrBlockedHost ───────────────────────────────────────────────

        [Fact]
        public void IsPrivateOrBlockedHost_AwsMetadataHost_Blocked()
        {
            var uri = new Uri("http://169.254.169.254/");
            Assert.True(SsrfValidator.IsPrivateOrBlockedHost(uri));
        }

        [Fact]
        public void IsPrivateOrBlockedHost_PublicHost_NotBlocked()
        {
            // This may invoke DNS, using a known stable IP-based URL to avoid flakiness
            var uri = new Uri("http://8.8.8.8/");
            Assert.False(SsrfValidator.IsPrivateOrBlockedHost(uri));
        }
    }
}
