using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kei.Base.Helper.Security
{
    /// <summary>
    /// Global security exception handler and anomalous request detector.
    /// Provides:
    /// <list type="bullet">
    ///   <item>Uniform, non-leaking error response for all unhandled exceptions</item>
    ///   <item>Security-specific exception type recognition (injection, SSRF, IDOR, etc.)</item>
    ///   <item>Anomalous request pattern detection (path traversal, suspicious headers, oversized payloads)</item>
    ///   <item>Sanitized, structured JSON error responses — no stack traces in production</item>
    ///   <item>Integration logging via <see cref="IAuditLogger"/></item>
    /// </list>
    /// Register via <see cref="SecurityExtensions.UseKeiBaseSecurity"/> or call
    /// <see cref="GlobalSecurityHandlerExtensions.UseGlobalSecurityHandler"/> directly.
    /// </summary>
    public class GlobalSecurityHandler
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalSecurityHandler> _logger;
        private readonly GlobalSecurityOptions _options;

        public GlobalSecurityHandler(
            RequestDelegate next,
            ILogger<GlobalSecurityHandler> logger,
            GlobalSecurityOptions options)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? new GlobalSecurityOptions();
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // ── Pre-request anomaly detection ──────────────────────────────────
            if (_options.EnableAnomalyDetection)
            {
                var anomaly = DetectAnomalousRequest(context);
                if (anomaly != null)
                {
                    _logger.LogWarning("[SECURITY] Anomalous request blocked. Reason: {Reason}. Path: {Path} | IP: {IP}",
                        anomaly, context.Request.Path, GetClientIp(context));

                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    await WriteSecureErrorResponse(context, 400,
                        "Bad Request", "The request was rejected due to a security policy violation.");
                    return;
                }
            }

            // ── Execute pipeline and catch unhandled exceptions ────────────────
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        // ─── Exception Handling ───────────────────────────────────────────────────

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var (statusCode, errorCategory, userFacingMessage) = ClassifyException(exception);

            var clientIp = GetClientIp(context);
            var requestId = context.TraceIdentifier;

            // Always log internally with full detail (NOT surfaced to client)
            _logger.LogError(exception,
                "[SECURITY] {Category} — Status {Status} — Path: {Path} — IP: {IP} — TraceId: {TraceId}",
                errorCategory, statusCode, context.Request.Path, clientIp, requestId);

            // Ensure response not yet started
            if (context.Response.HasStarted)
            {
                _logger.LogWarning("[SECURITY] Response already started; cannot write security error body.");
                return;
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            // Add security headers even on error responses
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Cache-Control"] = "no-store";

            await WriteSecureErrorResponse(context, statusCode, errorCategory, userFacingMessage, requestId);
        }

        /// <summary>
        /// Maps exception types to HTTP status codes, error categories, and safe user messages.
        /// Does NOT expose internal exception details to the client.
        /// </summary>
        private static (int StatusCode, string Category, string UserMessage) ClassifyException(Exception ex)
        {
            return ex switch
            {
                // Security violations → 400 Bad Request (never 500 — avoids leaking system info)
                ArgumentException { ParamName: var p } when
                    IsSecurityArgException(ex.Message) =>
                    (400, "SecurityViolation", "The request was rejected due to a security policy violation."),

                InvalidOperationException ioe when
                    IsSecurityOperationException(ioe.Message) =>
                    (400, "SecurityViolation", "The request was rejected due to a security policy violation."),

                // Authentication / Authorization
                UnauthorizedAccessException =>
                    (401, "Unauthorized", "Authentication is required to access this resource."),

                // IDOR / ownership violations surface as 403 Forbidden, NOT as 404
                // (to avoid timing differences that leak resource existence)
                ArgumentException { Message: var m } when m.Contains("Ownership") =>
                    (403, "Forbidden", "Access denied."),

                // Validation errors
                ArgumentNullException =>
                    (400, "ValidationError", "A required parameter was missing or null."),

                ArgumentException =>
                    (400, "ValidationError", "One or more request parameters are invalid."),

                // Not found
                KeyNotFoundException =>
                    (404, "NotFound", "The requested resource was not found."),

                // Catch-all: Return 500 with generic message only
                _ =>
                    (500, "InternalError", "An unexpected error occurred. Please try again later."),
            };
        }

        // ─── Anomaly Detection ────────────────────────────────────────────────────

        /// <summary>
        /// Inspects the incoming request for common anomalous/attack patterns.
        /// Returns a non-null string describing the anomaly if detected, null if clean.
        /// </summary>
        private string? DetectAnomalousRequest(HttpContext context)
        {
            var req = context.Request;
            var path = req.Path.Value ?? string.Empty;

            // Path traversal
            if (path.Contains("../") || path.Contains("..\\") || path.Contains("%2e%2e", StringComparison.OrdinalIgnoreCase))
                return "PathTraversal";

            // Null byte injection in path
            if (path.Contains('\0') || path.Contains("%00", StringComparison.OrdinalIgnoreCase))
                return "NullByteInjection";

            // Oversized request URI
            if (path.Length > _options.MaxRequestPathLength)
                return "OversizedPath";

            // Suspicious User-Agent (scanner fingerprints)
            if (req.Headers.TryGetValue("User-Agent", out var ua))
            {
                var uaValue = ua.ToString().ToLowerInvariant();
                if (IsScanner(uaValue))
                    return "ScannerUserAgent";
            }

            // HTTP response header injection attempts in controllable headers
            foreach (var header in _options.HeadersToInspect)
            {
                if (req.Headers.TryGetValue(header, out var value))
                {
                    var v = value.ToString();
                    if (v.Contains('\r') || v.Contains('\n') || v.Contains('\0'))
                        return $"HeaderInjection:{header}";
                }
            }

            // Oversized Content-Length
            if (req.ContentLength.HasValue && req.ContentLength.Value > _options.MaxBodyBytes)
                return "OversizedBody";

            return null;
        }

        private static readonly string[] _scannerTokens =
        {
            "sqlmap", "nikto", "nmap", "masscan", "nessus", "openvas", "burpsuite",
            "dirbuster", "dirb", "gobuster", "wfuzz", "hydra", "metasploit", "zgrab",
            "nuclei", "nuclei-", "python-requests", "go-http-client/1.1",
        };

        private static bool IsScanner(string ua)
        {
            foreach (var token in _scannerTokens)
                if (ua.Contains(token)) return true;
            return false;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static string GetClientIp(HttpContext context)
        {
            // Respect X-Forwarded-For if configured (for reverse proxy setups)
            if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
                return forwarded.ToString().Split(',')[0].Trim();

            return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        private static async Task WriteSecureErrorResponse(
            HttpContext context,
            int status,
            string category,
            string message,
            string? traceId = null)
        {
            var payload = new
            {
                Success = false,
                Status = status,
                Error = category,
                Message = message,
                TraceId = traceId ?? context.TraceIdentifier,
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });

            await context.Response.WriteAsync(json);
        }

        private static bool IsSecurityArgException(string message)
        {
            var m = message.ToLowerInvariant();
            return m.Contains("injection") || m.Contains("not in the allowed") ||
                   m.Contains("illegal characters") || m.Contains("ssrf") ||
                   m.Contains("blocked") || m.Contains("header");
        }

        private static bool IsSecurityOperationException(string message)
        {
            var m = message.ToLowerInvariant();
            return m.Contains("blocked") || m.Contains("injection") ||
                   m.Contains("ssrf") || m.Contains("security validation") ||
                   m.Contains("private") || m.Contains("scheme");
        }
    }

    // ─── Options ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Configuration options for <see cref="GlobalSecurityHandler"/>.
    /// </summary>
    public class GlobalSecurityOptions
    {
        /// <summary>Enable anomaly detection (path traversal, null bytes, scanners).</summary>
        public bool EnableAnomalyDetection { get; set; } = true;

        /// <summary>Maximum allowed request path length in characters.</summary>
        public int MaxRequestPathLength { get; set; } = 2048;

        /// <summary>Maximum allowed request body in bytes.</summary>
        public long MaxBodyBytes { get; set; } = 10 * 1024 * 1024; // 10 MB

        /// <summary>Headers to inspect for injection attempts.</summary>
        public IReadOnlyList<string> HeadersToInspect { get; set; } = new[]
        {
            "X-Forwarded-For", "X-Forwarded-Host", "Referer",
            "X-Real-IP", "X-Original-URL",
        };
    }

    // ─── Extension Methods ────────────────────────────────────────────────────────

    public static class GlobalSecurityHandlerExtensions
    {
        /// <summary>
        /// Registers the <see cref="GlobalSecurityHandler"/> in the middleware pipeline.
        /// Call this as early as possible — before routing and authorization.
        /// </summary>
        public static IApplicationBuilder UseGlobalSecurityHandler(
            this IApplicationBuilder app,
            Action<GlobalSecurityOptions>? configure = null)
        {
            var options = new GlobalSecurityOptions();
            configure?.Invoke(options);

            return app.UseMiddleware<GlobalSecurityHandler>(options);
        }

        /// <summary>
        /// Registers <see cref="GlobalSecurityOptions"/> in the DI container.
        /// </summary>
        public static IServiceCollection AddGlobalSecurityHandler(
            this IServiceCollection services,
            Action<GlobalSecurityOptions>? configure = null)
        {
            var options = new GlobalSecurityOptions();
            configure?.Invoke(options);
            services.AddSingleton(options);
            return services;
        }
    }
}
