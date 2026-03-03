using System;
using System.Security.Cryptography;
using System.Text;

namespace Kei.Base.Helper.Security
{
    /// <summary>
    /// Guards against IDOR (Insecure Direct Object Reference) by providing
    /// HMAC-based opaque IDs and ownership assertion helpers.
    /// </summary>
    public static class IdorGuard
    {
        private const int DefaultTokenLength = 32;

        // ─── Opaque ID (HMAC Token) ────────────────────────────────────────────────

        /// <summary>
        /// Converts an internal <paramref name="id"/> into an opaque, HMAC-SHA256-signed
        /// token that can be safely exposed in API responses without revealing the raw ID.
        /// </summary>
        /// <param name="id">The internal identifier (any object; <c>ToString()</c> is used).</param>
        /// <param name="secret">A secret key known only to the server. Must not be empty.</param>
        /// <returns>A URL-safe Base64 string combining the raw ID and its HMAC signature.</returns>
        public static string HashId(object id, string secret)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (string.IsNullOrEmpty(secret)) throw new ArgumentNullException(nameof(secret));

            var idStr = id.ToString()!;
            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var msgBytes = Encoding.UTF8.GetBytes(idStr);

            using var hmac = new HMACSHA256(keyBytes);
            var signature = hmac.ComputeHash(msgBytes);

            // Combine: base64(id) + "." + base64(signature)
            var encodedId = Convert.ToBase64String(Encoding.UTF8.GetBytes(idStr))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var encodedSig = Convert.ToBase64String(signature)
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            return $"{encodedId}.{encodedSig}";
        }

        /// <summary>
        /// Verifies that <paramref name="token"/> matches the expected HMAC for
        /// <paramref name="expectedId"/>. Uses constant-time comparison to prevent timing attacks.
        /// </summary>
        /// <param name="token">The opaque token produced by <see cref="HashId"/>.</param>
        /// <param name="expectedId">The internal ID to verify against.</param>
        /// <param name="secret">The server secret used when the token was created.</param>
        /// <returns><c>true</c> if the token is valid and matches; <c>false</c> otherwise.</returns>
        public static bool VerifyHashedId(string token, object expectedId, string secret)
        {
            if (string.IsNullOrEmpty(token) || expectedId == null || string.IsNullOrEmpty(secret))
                return false;

            try
            {
                // Re-compute the expected token and compare in constant time
                var expectedToken = HashId(expectedId, secret);
                var tokenBytes = Encoding.UTF8.GetBytes(token);
                var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);

                return CryptographicOperations.FixedTimeEquals(tokenBytes, expectedBytes);
            }
            catch
            {
                return false;
            }
        }

        // ─── Ownership Assertion ───────────────────────────────────────────────────

        /// <summary>
        /// Asserts that the resource owner ID returned by <paramref name="ownerSelector"/>
        /// matches <paramref name="currentUserId"/>.
        /// Throws <see cref="UnauthorizedAccessException"/> if it does not, preventing IDOR.
        /// </summary>
        /// <typeparam name="T">Type of the resource entity.</typeparam>
        /// <param name="entity">The resource entity to check.</param>
        /// <param name="ownerSelector">Function that extracts the owner's user ID from the entity.</param>
        /// <param name="currentUserId">The authenticated user's ID.</param>
        /// <exception cref="ArgumentNullException">When <paramref name="entity"/> or selectors are null.</exception>
        /// <exception cref="UnauthorizedAccessException">When the current user does not own the resource.</exception>
        public static void AssertOwnership<T>(T entity, Func<T, string> ownerSelector, string currentUserId)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (ownerSelector == null) throw new ArgumentNullException(nameof(ownerSelector));
            if (currentUserId == null) throw new ArgumentNullException(nameof(currentUserId));

            var ownerId = ownerSelector(entity);

            if (!string.Equals(ownerId, currentUserId, StringComparison.Ordinal))
                throw new UnauthorizedAccessException(
                    "Access denied: the current user does not own this resource (IDOR protection).");
        }

        /// <summary>
        /// Async-compatible overload of <see cref="AssertOwnership{T}"/>.
        /// </summary>
        public static System.Threading.Tasks.Task AssertOwnershipAsync<T>(
            T entity, Func<T, string> ownerSelector, string currentUserId)
        {
            AssertOwnership(entity, ownerSelector, currentUserId);
            return System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary>
        /// Returns <c>true</c> if the current user owns the resource; <c>false</c> otherwise.
        /// Useful when you want to check ownership without throwing an exception.
        /// </summary>
        public static bool IsOwner<T>(T entity, Func<T, string> ownerSelector, string currentUserId)
        {
            if (entity == null || ownerSelector == null || currentUserId == null)
                return false;

            var ownerId = ownerSelector(entity);
            return string.Equals(ownerId, currentUserId, StringComparison.Ordinal);
        }

        // ─── Resource Key Validation ──────────────────────────────────────────────

        /// <summary>
        /// Validates that the provided <paramref name="resourceId"/> is a positive integer,
        /// rejecting zero, negative, or non-numeric values. This prevents enumeration via
        /// negative or crafted ID values.
        /// </summary>
        public static void AssertValidId(object resourceId)
        {
            if (resourceId == null)
                throw new ArgumentNullException(nameof(resourceId), "Resource ID must not be null.");

            var idStr = resourceId.ToString();
            if (!long.TryParse(idStr, out var idVal) || idVal <= 0)
                throw new ArgumentException(
                    $"Resource ID '{idStr}' is invalid. Only positive integers are accepted.",
                    nameof(resourceId));
        }

        /// <summary>
        /// Validates that the provided <paramref name="resourceId"/> is a non-empty GUID.
        /// </summary>
        public static void AssertValidGuid(string resourceId)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
                throw new ArgumentNullException(nameof(resourceId), "Resource ID must not be null or empty.");

            if (!Guid.TryParse(resourceId, out var guid) || guid == Guid.Empty)
                throw new ArgumentException(
                    $"Resource ID '{resourceId}' is not a valid GUID.",
                    nameof(resourceId));
        }
    }
}
