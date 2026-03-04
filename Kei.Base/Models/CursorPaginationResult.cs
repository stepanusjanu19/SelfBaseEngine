using System.Collections.Generic;

namespace Kei.Base.Models
{
    /// <summary>
    /// Represents a paginated result set using cursor-based pagination (keyset pagination)
    /// which leverages database B-tree indexes for O(log n) performance on large datasets.
    /// </summary>
    /// <typeparam name="T">The type of the data list.</typeparam>
    public class CursorPaginationResult<T>
    {
        /// <summary>
        /// The paginated items returned for the current cursor condition.
        /// </summary>
        public IEnumerable<T> Data { get; set; } = new List<T>();

        /// <summary>
        /// The cursor value of the last item in the current <see cref="Data"/> list.
        /// Use this value in the next request to fetch the subsequent page.
        /// If null, there are no items strictly after this page.
        /// </summary>
        public object? NextCursor { get; set; }

        /// <summary>
        /// Indicates if there are more records beyond the current page.
        /// </summary>
        public bool HasNextPage { get; set; }
    }
}
