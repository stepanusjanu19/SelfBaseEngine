using System;
using System.Collections.Generic;
using System.Linq;
using Kei.Base.Helper;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Kei.Base.Helper
{
    [AttributeUsage(AttributeTargets.Property)]
    public class CsvHeaderAttribute : Attribute
    {
        public string Header { get; }
        public CsvHeaderAttribute(string header)
        {
            Header = header;
        }
    }

    public static class CsvColumnMapper
    {
        private static readonly Dictionary<Type, object> _mapCache = new();

        public static IEnumerable<(string Header, Func<T, object> Selector)> GetColumns<T>()
        {
            var type = typeof(T);
            if (_mapCache.TryGetValue(type, out var cached))
                return (IEnumerable<(string, Func<T, object>)>)cached;

            var props = type.GetProperties();

            var map = props.Select(p =>
            {
                var attr = p.GetCustomAttribute<CsvHeaderAttribute>();
                var header = attr?.Header ?? StringExtensions.ToUpperSnakeCase(p.Name);
                Func<T, object> selector = (T x) => p.GetValue(x);
                return (header, selector);
            }).ToList();

            _mapCache[type] = map;
            return map;
        }
    }
}
