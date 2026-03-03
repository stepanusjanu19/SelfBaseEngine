using System;
using System.Security.Cryptography;
using System.Text;

namespace Kei.Base.Helper.Security
{
    /// <summary>
    /// Provides cryptographically strong tokens, HMAC signatures, and
    /// password hashing (PBKDF2-SHA512) for use across the application.
    /// </summary>
    public static class SecureTokenHelper
    {
        private const int DefaultByteLength = 32;   // 256-bit token
        private const int DefaultPbkdf2Iterations = 200_000;

        // ─── Random Token Generation ───────────────────────────────────────────────

        /// <summary>
        /// Generates a cryptographically random URL-safe Base64 token.
        /// Default is 32 bytes (256 bits), producing a 43-character string.
        /// </summary>
        /// <param name="byteLength">Number of random bytes (default: 32).</param>
        public static string GenerateSecureToken(int byteLength = DefaultByteLength)
        {
            if (byteLength <= 0) throw new ArgumentOutOfRangeException(nameof(byteLength));

            var bytes = RandomNumberGenerator.GetBytes(byteLength);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Generates a cryptographically random hex string (2× <paramref name="byteLength"/> hex chars).
        /// </summary>
        public static string GenerateSecureHex(int byteLength = DefaultByteLength)
        {
            var bytes = RandomNumberGenerator.GetBytes(byteLength);
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        // ─── HMAC Signatures ───────────────────────────────────────────────────────

        /// <summary>
        /// Computes an HMAC-SHA256 signature for <paramref name="data"/> using <paramref name="secret"/>.
        /// Returns a URL-safe Base64 string suitable for use as a message authentication code.
        /// </summary>
        public static string GenerateHmacSignature(string data, string secret)
        {
            if (string.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            if (string.IsNullOrEmpty(secret)) throw new ArgumentNullException(nameof(secret));

            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var msgBytes = Encoding.UTF8.GetBytes(data);

            using var hmac = new HMACSHA256(keyBytes);
            var hash = hmac.ComputeHash(msgBytes);
            return Convert.ToBase64String(hash)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// Verifies an HMAC-SHA256 signature using constant-time comparison
        /// to prevent timing-oracle attacks.
        /// </summary>
        /// <returns><c>true</c> if the signature is valid; <c>false</c> otherwise.</returns>
        public static bool VerifyHmacSignature(string data, string signature, string secret)
        {
            if (string.IsNullOrEmpty(data) || string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
                return false;

            try
            {
                var expected = GenerateHmacSignature(data, secret);
                var expectedBytes = Encoding.UTF8.GetBytes(expected);
                var actualBytes = Encoding.UTF8.GetBytes(signature);
                return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
            }
            catch
            {
                return false;
            }
        }

        // ─── Password Hashing (PBKDF2-SHA512) ─────────────────────────────────────

        /// <summary>
        /// Hashes a password using PBKDF2-SHA512 with a random 16-byte salt and
        /// <see cref="DefaultPbkdf2Iterations"/> iterations.
        /// The returned string contains the iteration count, salt, and hash in a
        /// self-describing format: <c>{iterations}:{base64 salt}:{base64 hash}</c>.
        /// </summary>
        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            var salt = RandomNumberGenerator.GetBytes(16);

            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                DefaultPbkdf2Iterations,
                HashAlgorithmName.SHA512);

            var hashBytes = pbkdf2.GetBytes(64); // 512-bit output

            return $"{DefaultPbkdf2Iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hashBytes)}";
        }

        /// <summary>
        /// Verifies a password against a stored hash produced by <see cref="HashPassword"/>.
        /// Uses constant-time comparison to prevent timing attacks.
        /// </summary>
        /// <returns><c>true</c> if the password matches; <c>false</c> otherwise.</returns>
        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
                return false;

            try
            {
                var parts = storedHash.Split(':');
                if (parts.Length != 3) return false;

                if (!int.TryParse(parts[0], out var iterations) || iterations <= 0)
                    return false;

                var salt = Convert.FromBase64String(parts[1]);
                var expected = Convert.FromBase64String(parts[2]);

                using var pbkdf2 = new Rfc2898DeriveBytes(
                    password,
                    salt,
                    iterations,
                    HashAlgorithmName.SHA512);

                var actual = pbkdf2.GetBytes(expected.Length);
                return CryptographicOperations.FixedTimeEquals(expected, actual);
            }
            catch
            {
                return false;
            }
        }

        // ─── Secure Byte Clearing ──────────────────────────────────────────────────

        /// <summary>
        /// Zeroes a byte array in memory after use to reduce the window in which
        /// sensitive material (keys, plaintext) remains accessible.
        /// </summary>
        public static void SecureClear(byte[]? buffer)
        {
            if (buffer == null) return;
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}
