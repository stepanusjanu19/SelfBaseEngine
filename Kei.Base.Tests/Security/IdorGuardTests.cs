using System;
using Kei.Base.Helper.Security;

namespace Kei.Base.Tests.Security
{
    /// <summary>
    /// Unit tests for IdorGuard — covers opaque ID hashing, ownership assertion,
    /// and resource key validation with mock entity data.
    /// </summary>
    public class IdorGuardTests
    {
        private const string Secret = "test-secret-key-do-not-use-in-prod";
        private const string AltSecret = "different-secret-key";

        // ─── Mock Entity ──────────────────────────────────────────────────────────

        private record Order(int OrderId, string OwnerId, decimal Total);

        private static readonly Order _mockOrder = new(42, "user-abc", 199.99m);

        // ─── HashId / VerifyHashedId ──────────────────────────────────────────────

        [Fact]
        public void HashId_ProducesNonEmptyToken()
        {
            var token = IdorGuard.HashId(42, Secret);
            Assert.False(string.IsNullOrEmpty(token));
        }

        [Fact]
        public void HashId_SameIdAndSecret_ProduceSameToken()
        {
            var t1 = IdorGuard.HashId(42, Secret);
            var t2 = IdorGuard.HashId(42, Secret);
            Assert.Equal(t1, t2);
        }

        [Fact]
        public void HashId_DifferentId_ProducesDifferentToken()
        {
            var t1 = IdorGuard.HashId(42, Secret);
            var t2 = IdorGuard.HashId(99, Secret);
            Assert.NotEqual(t1, t2);
        }

        [Fact]
        public void HashId_DifferentSecret_ProducesDifferentToken()
        {
            var t1 = IdorGuard.HashId(42, Secret);
            var t2 = IdorGuard.HashId(42, AltSecret);
            Assert.NotEqual(t1, t2);
        }

        [Fact]
        public void HashId_TokenIsUrlSafe()
        {
            var token = IdorGuard.HashId(42, Secret);
            Assert.DoesNotContain("+", token);
            Assert.DoesNotContain("/", token);
            Assert.DoesNotContain("=", token);
        }

        [Fact]
        public void VerifyHashedId_ValidToken_ReturnsTrue()
        {
            var token = IdorGuard.HashId(42, Secret);
            Assert.True(IdorGuard.VerifyHashedId(token, 42, Secret));
        }

        [Fact]
        public void VerifyHashedId_WrongId_ReturnsFalse()
        {
            var token = IdorGuard.HashId(42, Secret);
            Assert.False(IdorGuard.VerifyHashedId(token, 43, Secret));
        }

        [Fact]
        public void VerifyHashedId_WrongSecret_ReturnsFalse()
        {
            var token = IdorGuard.HashId(42, Secret);
            Assert.False(IdorGuard.VerifyHashedId(token, 42, AltSecret));
        }

        [Fact]
        public void VerifyHashedId_TamperedToken_ReturnsFalse()
        {
            var token = IdorGuard.HashId(42, Secret);
            var tampered = token[..^3] + "XXX";
            Assert.False(IdorGuard.VerifyHashedId(tampered, 42, Secret));
        }

        [Fact]
        public void VerifyHashedId_EmptyToken_ReturnsFalse()
        {
            Assert.False(IdorGuard.VerifyHashedId("", 42, Secret));
        }

        [Fact]
        public void VerifyHashedId_NullSecret_ReturnsFalse()
        {
            Assert.False(IdorGuard.VerifyHashedId("sometoken", 42, null!));
        }

        // ─── AssertOwnership ──────────────────────────────────────────────────────

        [Fact]
        public void AssertOwnership_OwnerMatch_DoesNotThrow()
        {
            var ex = Record.Exception(() =>
                IdorGuard.AssertOwnership(_mockOrder, o => o.OwnerId, "user-abc"));
            Assert.Null(ex);
        }

        [Fact]
        public void AssertOwnership_WrongUser_Throws()
        {
            Assert.Throws<UnauthorizedAccessException>(() =>
                IdorGuard.AssertOwnership(_mockOrder, o => o.OwnerId, "user-xyz"));
        }

        [Fact]
        public void AssertOwnership_NullEntity_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                IdorGuard.AssertOwnership<Order>(null!, o => o.OwnerId, "user-abc"));
        }

        [Fact]
        public void AssertOwnership_NullUserId_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                IdorGuard.AssertOwnership(_mockOrder, o => o.OwnerId, null!));
        }

        // ─── IsOwner ─────────────────────────────────────────────────────────────

        [Fact]
        public void IsOwner_MatchingOwner_ReturnsTrue()
        {
            Assert.True(IdorGuard.IsOwner(_mockOrder, o => o.OwnerId, "user-abc"));
        }

        [Fact]
        public void IsOwner_WrongOwner_ReturnsFalse()
        {
            Assert.False(IdorGuard.IsOwner(_mockOrder, o => o.OwnerId, "user-xyz"));
        }

        // ─── AssertValidId ────────────────────────────────────────────────────────

        [Theory]
        [InlineData(1)]
        [InlineData(999)]
        [InlineData(int.MaxValue)]
        public void AssertValidId_PositiveInteger_DoesNotThrow(int id)
        {
            var ex = Record.Exception(() => IdorGuard.AssertValidId(id));
            Assert.Null(ex);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-999)]
        public void AssertValidId_NonPositive_Throws(int id)
        {
            Assert.Throws<ArgumentException>(() => IdorGuard.AssertValidId(id));
        }

        [Fact]
        public void AssertValidId_StringId_Throws()
        {
            Assert.Throws<ArgumentException>(() => IdorGuard.AssertValidId("not-a-number"));
        }

        [Fact]
        public void AssertValidId_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => IdorGuard.AssertValidId(null!));
        }

        // ─── AssertValidGuid ─────────────────────────────────────────────────────

        [Fact]
        public void AssertValidGuid_ValidGuid_DoesNotThrow()
        {
            var ex = Record.Exception(() =>
                IdorGuard.AssertValidGuid(Guid.NewGuid().ToString()));
            Assert.Null(ex);
        }

        [Fact]
        public void AssertValidGuid_EmptyGuid_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                IdorGuard.AssertValidGuid(Guid.Empty.ToString()));
        }

        [Fact]
        public void AssertValidGuid_NotAGuid_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                IdorGuard.AssertValidGuid("12345-not-a-guid"));
        }

        [Fact]
        public void AssertValidGuid_Empty_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                IdorGuard.AssertValidGuid(""));
        }
    }
}
