using System;

namespace Kei.Base.Domain.Entities
{
    /// <summary>
    /// Base entity class that uses ULID (Universally Unique Lexicographically Sortable Identifier)
    /// as its primary key. ULIDs are 128-bit compatible with UUIDs but are sortable.
    /// </summary>
    public abstract class BaseEntityUlid
    {
        /// <summary>
        /// The unique ULID identifier for the entity. Automatically generated on instantiation.
        /// </summary>
        public Ulid Id { get; set; } = Ulid.NewUlid();
    }
}
