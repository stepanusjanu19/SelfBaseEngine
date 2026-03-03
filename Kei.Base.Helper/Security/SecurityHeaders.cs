using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Kei.Base.Helper.Security
{
    /// <summary>
    /// Options for configuring the security response headers injected by
    /// <see cref="SecurityHeadersMiddleware"/>.
    /// </summary>
    public class SecurityHeadersOptions
    {
        /// <summary>
        /// Content-Security-Policy directive value.
        /// Default: strict policy suitable for REST APIs (no inline scripts or styles).
        /// </summary>
        public string ContentSecurityPolicy { get; set; } =
            "default-src 'none'; " +
            "script-src 'self'; " +
            "style-src 'self'; " +
            "img-src 'self' data:; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'; " +
            "form-action 'self'; " +
            "base-uri 'self'; " +
            "block-all-mixed-content;";

        /// <summary>
        /// Permissions-Policy directive value.
        /// Default: disables camera, microphone, geolocation, and payment APIs.
        /// </summary>
        public string PermissionsPolicy { get; set; } =
            "camera=(), microphone=(), geolocation=(), payment=(), usb=(), magnetometer=()";

        /// <summary>
        /// When <c>true</c>, adds <c>Cache-Control: no-store</c> to all responses.
        /// Recommended for API endpoints. Default: <c>true</c>.
        /// </summary>
        public bool NoCacheOnApiResponses { get; set; } = true;

        /// <summary>
        /// When <c>true</c>, removes the <c>Server</c> header to avoid server fingerprinting.
        /// Default: <c>true</c>.
        /// </summary>
        public bool RemoveServerHeader { get; set; } = true;
    }

    /// <summary>
    /// ASP.NET Core middleware that injects hardened security response headers
    /// to protect against XSS, clickjacking, MIME-sniffing, and information disclosure.
    ///
    /// Headers added:
    /// <list type="bullet">
    ///   <item><c>X-Content-Type-Options: nosniff</c></item>
    ///   <item><c>X-Frame-Options: DENY</c></item>
    ///   <item><c>X-XSS-Protection: 0</c> (modern browsers rely on CSP)</item>
    ///   <item><c>Referrer-Policy: strict-origin-when-cross-origin</c></item>
    ///   <item><c>Content-Security-Policy</c> (configurable)</item>
    ///   <item><c>Permissions-Policy</c> (configurable)</item>
    ///   <item><c>Cache-Control: no-store</c> (configurable)</item>
    ///   <item><c>Strict-Transport-Security</c> (for HTTPS requests)</item>
    /// </list>
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly SecurityHeadersOptions _options;

        public SecurityHeadersMiddleware(RequestDelegate next, SecurityHeadersOptions options)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _options = options ?? new SecurityHeadersOptions();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;

                // Prevent MIME type sniffing
                headers["X-Content-Type-Options"] = "nosniff";

                // Prevent clickjacking
                headers["X-Frame-Options"] = "DENY";

                // Modern browsers use CSP; keep this for legacy browsers
                headers["X-XSS-Protection"] = "0";

                // Referrer policy — don't leak full URL to external origins
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

                // Content Security Policy
                if (!string.IsNullOrEmpty(_options.ContentSecurityPolicy))
                    headers["Content-Security-Policy"] = _options.ContentSecurityPolicy;

                // Permissions Policy (formerly Feature-Policy)
                if (!string.IsNullOrEmpty(_options.PermissionsPolicy))
                    headers["Permissions-Policy"] = _options.PermissionsPolicy;

                // HSTS — only on HTTPS
                if (context.Request.IsHttps)
                    headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";

                // Cache control
                if (_options.NoCacheOnApiResponses)
                    headers["Cache-Control"] = "no-store";

                // Remove server fingerprint headers
                if (_options.RemoveServerHeader)
                {
                    headers.Remove("Server");
                    headers.Remove("X-Powered-By");
                    headers.Remove("X-AspNet-Version");
                    headers.Remove("X-AspNetMvc-Version");
                }

                return Task.CompletedTask;
            });

            await _next(context);
        }
    }

    /// <summary>
    /// Extension methods for integrating <see cref="SecurityHeadersMiddleware"/>
    /// into the ASP.NET Core pipeline.
    /// </summary>
    public static class SecurityHeadersApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds the <see cref="SecurityHeadersMiddleware"/> to the request pipeline
        /// with default options.
        /// </summary>
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
            => app.UseSecurityHeaders(new SecurityHeadersOptions());

        /// <summary>
        /// Adds the <see cref="SecurityHeadersMiddleware"/> to the request pipeline
        /// with custom <paramref name="options"/>.
        /// </summary>
        public static IApplicationBuilder UseSecurityHeaders(
            this IApplicationBuilder app,
            SecurityHeadersOptions options)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            return app.UseMiddleware<SecurityHeadersMiddleware>(options);
        }

        /// <summary>
        /// Adds the <see cref="SecurityHeadersMiddleware"/> to the request pipeline
        /// with options configured via a builder action.
        /// </summary>
        public static IApplicationBuilder UseSecurityHeaders(
            this IApplicationBuilder app,
            Action<SecurityHeadersOptions> configure)
        {
            var options = new SecurityHeadersOptions();
            configure?.Invoke(options);
            return app.UseSecurityHeaders(options);
        }
    }
}
