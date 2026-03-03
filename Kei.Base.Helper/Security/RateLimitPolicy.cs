using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Kei.Base.Helper.Security
{
    /// <summary>
    /// Configuration options for the sliding-window rate limiter.
    /// </summary>
    public class RateLimitOptions
    {
        /// <summary>
        /// Size of the sliding window (default: 60 seconds).
        /// </summary>
        public TimeSpan Window { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Maximum number of requests allowed within the <see cref="Window"/> (default: 100).
        /// </summary>
        public int MaxRequests { get; set; } = 100;

        /// <summary>
        /// Function that extracts the client identifier from an <see cref="HttpContext"/>
        /// (default: remote IP address).
        /// </summary>
        public Func<HttpContext, string> ClientIdentifier { get; set; } =
            ctx => ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// In-memory sliding-window rate limiter. Thread-safe.
    /// Stores per-client request timestamps and discards those outside the current window.
    /// </summary>
    public class SlidingWindowRateLimiter
    {
        private readonly RateLimitOptions _options;
        private readonly ConcurrentDictionary<string, Queue<DateTime>> _clientQueues = new();
        private readonly SemaphoreSlim _cleanupLock = new(1, 1);
        private DateTime _lastCleanup = DateTime.UtcNow;

        public SlidingWindowRateLimiter(RateLimitOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Records a request for <paramref name="clientId"/> and returns whether
        /// the request is within the allowed rate.
        /// </summary>
        /// <returns><c>true</c> if the request is allowed; <c>false</c> if the limit is exceeded.</returns>
        public bool IsAllowed(string clientId)
        {
            if (string.IsNullOrEmpty(clientId)) return true;

            var now = DateTime.UtcNow;
            var cutoff = now - _options.Window;

            var queue = _clientQueues.GetOrAdd(clientId, _ => new Queue<DateTime>());

            lock (queue)
            {
                // Discard timestamps outside the sliding window
                while (queue.Count > 0 && queue.Peek() < cutoff)
                    queue.Dequeue();

                if (queue.Count >= _options.MaxRequests)
                    return false;

                queue.Enqueue(now);
            }

            // Periodic cleanup to prevent unbounded memory growth
            TryCleanup(now);
            return true;
        }

        private void TryCleanup(DateTime now)
        {
            if ((now - _lastCleanup).TotalSeconds < 300) return; // cleanup every 5 min

            if (!_cleanupLock.Wait(0)) return; // skip if another cleanup is running
            try
            {
                _lastCleanup = now;
                var cutoff = now - _options.Window;

                foreach (var key in _clientQueues.Keys)
                {
                    if (_clientQueues.TryGetValue(key, out var q))
                    {
                        lock (q)
                        {
                            while (q.Count > 0 && q.Peek() < cutoff)
                                q.Dequeue();

                            if (q.Count == 0)
                                _clientQueues.TryRemove(key, out _);
                        }
                    }
                }
            }
            finally
            {
                _cleanupLock.Release();
            }
        }
    }

    /// <summary>
    /// Action filter attribute that enforces a per-endpoint sliding-window rate limit.
    ///
    /// Usage:
    /// <code>[Throttle(MaxRequests = 10, WindowSeconds = 60)]</code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class ThrottleAttribute : Attribute, IAsyncActionFilter
    {
        /// <summary>Maximum requests allowed within <see cref="WindowSeconds"/>.</summary>
        public int MaxRequests { get; set; } = 60;

        /// <summary>Sliding window size in seconds.</summary>
        public int WindowSeconds { get; set; } = 60;

        // Per-attribute-instance limiter (keyed by client ID)
        private SlidingWindowRateLimiter? _limiter;
        private readonly object _initLock = new();

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            EnsureLimiter();

            var options = new RateLimitOptions
            {
                MaxRequests = MaxRequests,
                Window = TimeSpan.FromSeconds(WindowSeconds)
            };

            var clientId = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            if (!_limiter!.IsAllowed(clientId))
            {
                context.Result = new ObjectResult(new { Success = false, Message = "Too many requests. Please slow down." })
                {
                    StatusCode = StatusCodes.Status429TooManyRequests
                };
                context.HttpContext.Response.Headers["Retry-After"] = WindowSeconds.ToString();
                return;
            }

            await next();
        }

        private void EnsureLimiter()
        {
            if (_limiter != null) return;
            lock (_initLock)
            {
                if (_limiter != null) return;
                _limiter = new SlidingWindowRateLimiter(new RateLimitOptions
                {
                    MaxRequests = MaxRequests,
                    Window = TimeSpan.FromSeconds(WindowSeconds)
                });
            }
        }
    }

    /// <summary>
    /// Extension methods for registering rate limit services.
    /// </summary>
    public static class RateLimitServiceExtensions
    {
        /// <summary>
        /// Registers a singleton <see cref="SlidingWindowRateLimiter"/> with default options.
        /// </summary>
        public static IServiceCollection AddBaseRateLimiting(this IServiceCollection services)
            => services.AddBaseRateLimiting(new RateLimitOptions());

        /// <summary>
        /// Registers a singleton <see cref="SlidingWindowRateLimiter"/> with custom options.
        /// </summary>
        public static IServiceCollection AddBaseRateLimiting(
            this IServiceCollection services,
            RateLimitOptions options)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            services.AddSingleton(options);
            services.AddSingleton<SlidingWindowRateLimiter>();
            return services;
        }

        /// <summary>
        /// Registers a singleton <see cref="SlidingWindowRateLimiter"/> configured via an action.
        /// </summary>
        public static IServiceCollection AddBaseRateLimiting(
            this IServiceCollection services,
            Action<RateLimitOptions> configure)
        {
            var opts = new RateLimitOptions();
            configure?.Invoke(opts);
            return services.AddBaseRateLimiting(opts);
        }
    }
}
