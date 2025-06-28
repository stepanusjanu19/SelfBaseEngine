using Kei.Base.Helper;
using System;
using Kei.Base.Extensions;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Kei.Base.Domain.Functions
{
    public class FilterBuilder<TEntity>
    {
        private readonly List<(Expression<Func<TEntity, object>> Property, object? Value, FilterOperator Operator, bool IsOr)> _filters = new();

        public FilterBuilder<TEntity> Add(
            Expression<Func<TEntity, object>> property,
            object? value,
            FilterOperator op,
            bool isOr = false)
        {
            if (value != null && (!(value is string s) || !string.IsNullOrWhiteSpace(s)))
            {
                _filters.Add((property, value, op, isOr));
            }
            return this;
        }

        public FilterBuilder<TEntity> AddList<TProp>(
            Expression<Func<TEntity, TProp>> property,
            IEnumerable<TProp> values,
            FilterOperator op = FilterOperator.In,
            bool isOr = false)
        {
            if (values != null && values.Any())
            {
                _filters.Add((CastToObject(property), values, op, isOr));
            }
            return this;
        }

        protected Expression<Func<TEntity, object>> CastToObject<TProp>(Expression<Func<TEntity, TProp>> expr)
        {
            var parameter = expr.Parameters[0];
            Expression body = expr.Body.Type == typeof(object)
                ? expr.Body
                : Expression.Convert(expr.Body, typeof(object));
            return Expression.Lambda<Func<TEntity, object>>(body, parameter);
        }

        internal (Expression<Func<TEntity, object>> Property, object? Value, FilterOperator Operator, bool IsOr)[] ToArray()
            => _filters.ToArray();
    }

    public static class EnumerableExtensions
    {
        public static List<T> CastToList<T>(this IEnumerable source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return source.Cast<T>().ToList();
        }

        public static IList CastToList(this IEnumerable source, Type targetType)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));

            var elementType = source.GetType().IsGenericType
                        ? source.GetType().GetGenericArguments()[0]
                        : null;

            if (elementType != null && typeof(IEnumerable).IsAssignableFrom(elementType) && elementType != typeof(string))
            {
                throw new InvalidCastException($"Cannot cast from IEnumerable<{elementType.Name}> to List<{targetType.Name}>. Possibly a nested list?");
            }

            var castMethod = typeof(Enumerable)
                .GetMethod(nameof(Enumerable.Cast))!
                .MakeGenericMethod(targetType);

            var toListMethod = typeof(Enumerable)
                .GetMethod(nameof(Enumerable.ToList))!
                .MakeGenericMethod(targetType);

            var casted = castMethod.Invoke(null, new object[] { source })!;
            return (IList)toListMethod.Invoke(null, new object[] { casted })!;
        }
    }
}
