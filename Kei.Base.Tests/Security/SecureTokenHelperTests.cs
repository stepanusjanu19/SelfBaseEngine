using System;
using Kei.Base.Helper.Security;

namespace Kei.Base.Tests.Security
{
    /// <summary>
    /// Unit tests for SecureTokenHelper — covers CSPRNG token generation,
    /// HMAC signature generation/verification, and PBKDF2 password hashing.
    /// </summary>
    public class SecureTokenHelperTests
    {
        // ─── GenerateSecureToken ──────────────────────────────────────────────────

        [Fact]
        public void GenerateSecureToken_DefaultLength_ProducesNonEmptyString()
        {
            var token = SecureTokenHelper.GenerateSecureToken();
            Assert.False(string.IsNullOrEmpty(token));
        }

        [Fact]
        public void GenerateSecureToken_TwoTokens_AreUnique()
        {
            var t1 = SecureTokenHelper.GenerateSecureToken();
            var t2 = SecureTokenHelper.GenerateSecureToken();
            Assert.NotEqual(t1, t2);
        }

        [Fact]
        public void GenerateSecureToken_IsUrlSafe()
        {
            var token = SecureTokenHelper.GenerateSecureToken();
            Assert.DoesNotContain("+", token);
            Assert.DoesNotContain("/", token);
            Assert.DoesNotContain("=", token);
        }

        [Theory]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(64)]
        public void GenerateSecureToken_CustomLength_ProducesToken(int bytes)
        {
            var token = SecureTokenHelper.GenerateSecureToken(bytes);
            Assert.False(string.IsNullOrEmpty(token));
        }

        [Fact]
        public void GenerateSecureToken_ZeroLength_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SecureTokenHelper.GenerateSecureToken(0));
        }

        // ─── GenerateSecureHex ────────────────────────────────────────────────────

        [Fact]
        public void GenerateSecureHex_ProducesHexString()
        {
            var hex = SecureTokenHelper.GenerateSecureHex(16);
            Assert.Equal(32, hex.Length); // 16 bytes = 32 hex chars
            Assert.Matches(@"^[0-9a-f]+$", hex);
        }

        // ─── GenerateHmacSignature / VerifyHmacSignature ──────────────────────────

        [Fact]
        public void GenerateHmacSignature_SameData_SameSignature()
        {
            var sig1 = SecureTokenHelper.GenerateHmacSignature("hello", "secret");
            var sig2 = SecureTokenHelper.GenerateHmacSignature("hello", "secret");
            Assert.Equal(sig1, sig2);
        }

        [Fact]
        public void GenerateHmacSignature_DifferentData_DifferentSignature()
        {
            var sig1 = SecureTokenHelper.GenerateHmacSignature("hello", "secret");
            var sig2 = SecureTokenHelper.GenerateHmacSignature("world", "secret");
            Assert.NotEqual(sig1, sig2);
        }

        [Fact]
        public void VerifyHmacSignature_ValidSignature_ReturnsTrue()
        {
            var data = "payload=order&amount=100";
            var key = "signing-key-32-bytes-min-for-safe";
            var sig = SecureTokenHelper.GenerateHmacSignature(data, key);
            Assert.True(SecureTokenHelper.VerifyHmacSignature(data, sig, key));
        }

        [Fact]
        public void VerifyHmacSignature_TamperedData_ReturnsFalse()
        {
            var key = "signing-key";
            var sig = SecureTokenHelper.GenerateHmacSignature("original", key);
            Assert.False(SecureTokenHelper.VerifyHmacSignature("tampered", sig, key));
        }

        [Fact]
        public void VerifyHmacSignature_TamperedSignature_ReturnsFalse()
        {
            var key = "signing-key";
            var sig = SecureTokenHelper.GenerateHmacSignature("data", key);
            var tampered = sig[..^3] + "XXX";
            Assert.False(SecureTokenHelper.VerifyHmacSignature("data", tampered, key));
        }

        [Fact]
        public void VerifyHmacSignature_WrongKey_ReturnsFalse()
        {
            var sig = SecureTokenHelper.GenerateHmacSignature("data", "correct-key");
            Assert.False(SecureTokenHelper.VerifyHmacSignature("data", sig, "wrong-key"));
        }

        [Fact]
        public void VerifyHmacSignature_EmptyInputs_ReturnsFalse()
        {
            Assert.False(SecureTokenHelper.VerifyHmacSignature("", "sig", "key"));
            Assert.False(SecureTokenHelper.VerifyHmacSignature("data", "", "key"));
            Assert.False(SecureTokenHelper.VerifyHmacSignature("data", "sig", ""));
        }

        // ─── HashPassword / VerifyPassword ────────────────────────────────────────

        [Fact]
        public void HashPassword_ProducesSelfDescribingHash()
        {
            var hash = SecureTokenHelper.HashPassword("P@ssw0rd!");
            var parts = hash.Split(':');
            Assert.Equal(3, parts.Length);
            Assert.True(int.TryParse(parts[0], out var iter) && iter > 0);
        }

        [Fact]
        public void HashPassword_TwoCalls_ProduceDifferentHashes()
        {
            // Different salts should produce different hashes even for same password
            var h1 = SecureTokenHelper.HashPassword("same-password");
            var h2 = SecureTokenHelper.HashPassword("same-password");
            Assert.NotEqual(h1, h2);
        }

        [Fact]
        public void VerifyPassword_CorrectPassword_ReturnsTrue()
        {
            var hash = SecureTokenHelper.HashPassword("SuperSecret123!");
            Assert.True(SecureTokenHelper.VerifyPassword("SuperSecret123!", hash));
        }

        [Fact]
        public void VerifyPassword_WrongPassword_ReturnsFalse()
        {
            var hash = SecureTokenHelper.HashPassword("RealPassword");
            Assert.False(SecureTokenHelper.VerifyPassword("WrongPassword", hash));
        }

        [Fact]
        public void VerifyPassword_EmptyPassword_ReturnsFalse()
        {
            var hash = SecureTokenHelper.HashPassword("password");
            Assert.False(SecureTokenHelper.VerifyPassword("", hash));
        }

        [Fact]
        public void VerifyPassword_MalformedHash_ReturnsFalse()
        {
            Assert.False(SecureTokenHelper.VerifyPassword("password", "not-a-valid-hash"));
        }

        [Fact]
        public void VerifyPassword_TamperedHash_ReturnsFalse()
        {
            var hash = SecureTokenHelper.HashPassword("password123");
            var tampered = hash.Replace(hash[^5..], "XXXXX");
            Assert.False(SecureTokenHelper.VerifyPassword("password123", tampered));
        }

        // ─── SecureClear ─────────────────────────────────────────────────────────

        [Fact]
        public void SecureClear_ZeroesBuffer()
        {
            var buf = new byte[] { 1, 2, 3, 0xFF };
            SecureTokenHelper.SecureClear(buf);
            Assert.All(buf, b => Assert.Equal(0, b));
        }

        [Fact]
        public void SecureClear_NullBuffer_DoesNotThrow()
        {
            var ex = Record.Exception(() => SecureTokenHelper.SecureClear(null));
            Assert.Null(ex);
        }
    }
}
