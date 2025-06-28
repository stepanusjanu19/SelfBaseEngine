using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Kei.Base.Helper
{
    public enum FilterOperator
    {
        Equal,
        NotEqual,
        Contains,
        StartsWith,
        EndsWith,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        In,
        NotIn,
        IsNull,
        IsNotNull,
        Between,
        NotLike
    }

    public class FilterCondition<TEntity>
    {
        public Expression<Func<TEntity, object>> PropertyExpression { get; set; }
        public string PropertyName { get; set; }
        public object Value { get; set; }
        public FilterOperator Operator { get; set; } = FilterOperator.Equal;
        public bool? IsOr { get; set; } = false;
    }
}
