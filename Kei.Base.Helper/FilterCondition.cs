using System;
using System.Collections.Generic;
using System.Linq;
using Kei.Base.Extensions;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Kei.Base.Helper
{
    /// <summary>
    /// When applied to an entity property, indicates that this property is explicitly
    /// permitted in dynamic filter operations (e.g., <c>GetQueryableByFilter</c>).
    /// Use in combination with <see cref="Security.SqlGuard.AssertSafeColumnName"/> to
    /// enforce a type-safe allowlist.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class AllowedFilterAttribute : Attribute
    {
        /// <summary>Optional description of why this field is safe for dynamic filtering.</summary>
        public string? Description { get; }

        public AllowedFilterAttribute(string? description = null)
        {
            Description = description;
        }
    }

    public class FilterCondition<TEntity>
    {
        public Expression<Func<TEntity, object>> PropertyExpression { get; set; }
        public string PropertyName { get; set; }
        public object Value { get; set; }
        public FilterOperator Operator { get; set; } = FilterOperator.Equal;
        public bool? IsOr { get; set; } = false;
        public List<FilterCondition<TEntity>>? GroupConditions { get; set; }
    }
}
