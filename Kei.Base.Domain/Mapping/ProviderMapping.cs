using AutoMapper;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Kei.Base.Domain.Mapping
{
    public static class MappingConfigProvider
    {

        private static readonly object _lock = new();
        private static bool _initialized = false;
        private static IMapper? _mapper;
        private static IConfigurationProvider? _configuration;
        private static readonly ConcurrentDictionary<string, object> _cache = new();

        // === GLOBAL INIT ===
        public static void InitializeGlobal(Assembly assembly)
        {
            lock (_lock)
            {
                if (_initialized) return;

                var profiles = assembly.GetTypes()
                    .Where(t => typeof(Profile).IsAssignableFrom(t) &&
                                !t.IsAbstract &&
                                !t.IsGenericTypeDefinition);

                var config = new MapperConfiguration(cfg =>
                {
                    foreach (var profileType in profiles)
                    {
                        if (Activator.CreateInstance(profileType) is Profile profile)
                            cfg.AddProfile(profile);
                    }
                });

                config.AssertConfigurationIsValid();

                _configuration = config;
                _mapper = config.CreateMapper();
                _initialized = true;
            }
        }

        private static void EnsureInitialized()
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;
                InitializeGlobal(Assembly.GetExecutingAssembly());
            }
        }

        public static IMapper Mapper
        {
            get
            {
                EnsureInitialized();
                return _mapper!;
            }
        }

        public static IConfigurationProvider Configuration
        {
            get
            {
                EnsureInitialized();
                return _configuration!;
            }
        }

        // === GENERIC FACTORIES ===
        private static IConfigurationProvider CreateConfig<TProfile, TSource, TDestination>(string keyPrefix)
            where TProfile : Profile, new()
        {
            var key = $"{keyPrefix}:config:{typeof(TSource).FullName}->{typeof(TDestination).FullName}";
            return (IConfigurationProvider)_cache.GetOrAdd(key, _ =>
                new MapperConfiguration(cfg => cfg.AddProfile(new TProfile())));
        }

        private static IMapper CreateMapper<TProfile, TSource, TDestination>(string keyPrefix)
            where TProfile : Profile, new()
        {
            var key = $"{keyPrefix}:mapper:{typeof(TSource).FullName}->{typeof(TDestination).FullName}";
            return (IMapper)_cache.GetOrAdd(key, _ =>
                new MapperConfiguration(cfg => cfg.AddProfile(new TProfile())).CreateMapper());
        }

        // === SAFE MAPPINGS ===
        public static IConfigurationProvider GetSafeConfig<TSource, TDestination>() =>
            CreateConfig<SafeMappingProfile<TSource, TDestination>, TSource, TDestination>("safe");

        public static IMapper ToSafeMapper<TSource, TDestination>() =>
            CreateMapper<SafeMappingProfile<TSource, TDestination>, TSource, TDestination>("safe");

        // === PLAIN MAPPINGS ===
        public static IConfigurationProvider GetPlainConfig<TSource, TDestination>() =>
            CreateConfig<PlainMappingProfile<TSource, TDestination>, TSource, TDestination>("plain");

        public static IMapper ToPlainMapper<TSource, TDestination>() =>
            CreateMapper<PlainMappingProfile<TSource, TDestination>, TSource, TDestination>("plain");

        // === SELF MAPPINGS ===
        public static IConfigurationProvider GetSelfConfig<T>() =>
            GetPlainConfig<T, T>();

        public static IMapper ToSelfMapper<T>() =>
            ToPlainMapper<T, T>();

        // === CROSS MAPPINGS ===

        public static IConfigurationProvider GetCrossConfig<TSource, TDestination>() =>
            CreateConfig<CrossMappingProfile<TSource, TDestination>, TSource, TDestination>("cross");

        public static IMapper ToCrossMapper<TSource, TDestination>() =>
            CreateMapper<CrossMappingProfile<TSource, TDestination>, TSource, TDestination>("cross");

        // === GENERIC MAPPINGS ===

        public static IConfigurationProvider GetGenericConfig<TSource, TDestination>() =>
            CreateConfig<GenericMappingProfile<TSource, TDestination>, TSource, TDestination>("generic");

        public static IMapper ToGenericMapper<TSource, TDestination>() =>
            CreateMapper<GenericMappingProfile<TSource, TDestination>, TSource, TDestination>("generic");

    }
}
