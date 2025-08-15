using System;
using System.Collections.Generic;
using System.Linq;
using Kei.Base.Extensions;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Kei.Base.Helper
{
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
