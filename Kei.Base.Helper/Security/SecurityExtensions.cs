using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Kei.Base.Helper.Security
{
    /// <summary>
    /// Aggregate options for the Kei.Base security suite.
    /// Configure once via <see cref="SecurityServiceExtensions.AddKeiBaseSecurity"/>.
    /// </summary>
    public class KeiBaseSecurityOptions
    {
        /// <summary>
        /// Hosts allowed for outbound HTTP calls (SSRF protection).
        /// An empty list means the SSRF allowlist is open — always set this in production.
        /// </summary>
        public IList<string> AllowedHosts { get; set; } = new List<string>();

        /// <summary>Whether to register the in-memory rate limiter. Default: <c>true</c>.</summary>
        public bool EnableRateLimit { get; set; } = true;

        /// <summary>Options for rate limiting.</summary>
        public RateLimitOptions? RateLimitOptions { get; set; }

        /// <summary>Whether to register the security audit logger. Default: <c>true</c>.</summary>
        public bool EnableAuditLog { get; set; } = true;

        /// <summary>Options for audit logging.</summary>
        public AuditOptions? AuditOptions { get; set; }

        /// <summary>Options for the security headers middleware.</summary>
        public SecurityHeadersOptions? HeaderOptions { get; set; }
    }

    /// <summary>
    /// Extension methods that provide a single-line bootstrap for all Kei.Base
    /// backend security features:
    ///
    /// <code>
    /// // In Program.cs / Startup.cs:
    /// builder.Services.AddKeiBaseSecurity(options =>
    /// {
    ///     options.AllowedHosts     = new[] { "api.example.com" };
    ///     options.EnableRateLimit  = true;
    ///     options.EnableAuditLog   = true;
    /// });
    ///
    /// // In the middleware pipeline:
    /// app.UseKeiBaseSecurity();
    /// </code>
    /// </summary>
    public static class SecurityServiceExtensions
    {
        // ─── DI Registration ──────────────────────────────────────────────────────

        /// <summary>
        /// Registers all Kei.Base security services with default options.
        /// </summary>
        public static IServiceCollection AddKeiBaseSecurity(this IServiceCollection services)
            => services.AddKeiBaseSecurity(_ => { });

        /// <summary>
        /// Registers all Kei.Base security services with options configured via <paramref name="configure"/>.
        /// </summary>
        public static IServiceCollection AddKeiBaseSecurity(
            this IServiceCollection services,
            Action<KeiBaseSecurityOptions> configure)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            var opts = new KeiBaseSecurityOptions();
            configure?.Invoke(opts);

            // Store the aggregate options for use by middleware
            services.AddSingleton(opts);
            services.AddSingleton(opts.HeaderOptions ?? new SecurityHeadersOptions());

            // Rate limiting
            if (opts.EnableRateLimit)
            {
                var rlOpts = opts.RateLimitOptions ?? new RateLimitOptions();
                services.AddSingleton(rlOpts);
                services.AddSingleton<SlidingWindowRateLimiter>();
            }

            // Audit logging
            if (opts.EnableAuditLog)
            {
                var auditOpts = opts.AuditOptions ?? new AuditOptions();
                services.AddSingleton(auditOpts);
                services.AddSingleton<IAuditLogger, ConsoleAuditLogger>();
            }

            return services;
        }

        // ─── Pipeline Integration ──────────────────────────────────────────────────

        /// <summary>
        /// Adds the Kei.Base security middleware pipeline:
        /// <list type="bullet">
        ///   <item><see cref="GlobalSecurityHandler"/> — anomaly detection + uniform exception handling</item>
        ///   <item><see cref="SecurityHeadersMiddleware"/> — security response headers</item>
        /// </list>
        /// Call this early in your middleware pipeline, before routing/controllers.
        /// </summary>
        public static IApplicationBuilder UseKeiBaseSecurity(this IApplicationBuilder app)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));

            // 1. Global security handler must be outermost — catches all downstream exceptions
            var globalOpts = app.ApplicationServices
                .GetService<GlobalSecurityOptions>() ?? new GlobalSecurityOptions();
            app.UseMiddleware<GlobalSecurityHandler>(globalOpts);

            // 2. Security response headers
            var headerOptions = app.ApplicationServices
                .GetService<SecurityHeadersOptions>() ?? new SecurityHeadersOptions();
            app.UseMiddleware<SecurityHeadersMiddleware>(headerOptions);

            return app;
        }

        /// <summary>
        /// Adds the Kei.Base security middleware pipeline with custom header options.
        /// </summary>
        public static IApplicationBuilder UseKeiBaseSecurity(
            this IApplicationBuilder app,
            Action<SecurityHeadersOptions> configureHeaders)
        {
            var opts = new SecurityHeadersOptions();
            configureHeaders?.Invoke(opts);
            return app.UseMiddleware<SecurityHeadersMiddleware>(opts);
        }
    }
}
