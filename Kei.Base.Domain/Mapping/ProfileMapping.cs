using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kei.Base.Domain.Mapping
{
    public class SafeMappingProfile<TSource, TDestination> : Profile
    {
        public SafeMappingProfile()
        {
            CreateMap<TSource, TDestination>()
                .AfterMap((src, dest) =>
                {
                    foreach (var prop in typeof(TDestination).GetProperties())
                    {
                        if (prop.GetValue(dest) == null)
                            prop.SetValue(dest, GetDefault(prop.PropertyType));
                    }
                });
        }

        private static object? GetDefault(Type type) =>
            type == typeof(string) ? string.Empty :
            type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    public class PlainMappingProfile<TSource, TDestination> : Profile
    {
        public PlainMappingProfile()
        {
            CreateMap<TSource, TDestination>();
        }
    }

    public class CrossMappingProfile<TSource, TDestination> : Profile
    {
        public CrossMappingProfile()
        {
            CreateMap<TSource, TDestination>();
            CreateMap<TDestination, TSource>();
        }
    }

    public class GenericMappingProfile<TSource, TDestination> : Profile
    {
        public GenericMappingProfile()
        {
            CreateMap<TSource, TDestination>();
        }
    }
}
