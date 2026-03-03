using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kei.Base.Helper.Security
{
    /// <summary>
    /// Options controlling what the <see cref="SecurityAuditAttribute"/> captures.
    /// </summary>
    public class AuditOptions
    {
        /// <summary>Log the request IP address (default: <c>true</c>).</summary>
        public bool LogIpAddress { get; set; } = true;

        /// <summary>Log the authenticated user identity (default: <c>true</c>).</summary>
        public bool LogUserIdentity { get; set; } = true;

        /// <summary>Log the action name (default: <c>true</c>).</summary>
        public bool LogActionName { get; set; } = true;

        /// <summary>Log the response HTTP status code (default: <c>true</c>).</summary>
        public bool LogStatusCode { get; set; } = true;

        /// <summary>Log action arguments (be careful with sensitive inputs; default: <c>false</c>).</summary>
        public bool LogActionArguments { get; set; } = false;
    }

    /// <summary>
    /// Contract for security audit loggers.
    /// Implement this interface and register via <see cref="SecurityAuditExtensions.AddSecurityAudit"/>
    /// to redirect audit events to any sink (database, ELK, SIEM, etc.).
    /// </summary>
    public interface IAuditLogger
    {
        /// <summary>
        /// Records a security audit event.
        /// </summary>
        Task LogAsync(AuditEvent auditEvent);
    }

    /// <summary>
    /// Represents a single captured security audit event.
    /// </summary>
    public class AuditEvent
    {
        /// <summary>UTC timestamp of the request.</summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        /// <summary>Client IP address.</summary>
        public string? IpAddress { get; init; }
        /// <summary>Authenticated user identity name.</summary>
        public string? UserIdentity { get; init; }
        /// <summary>Controller.Action name.</summary>
        public string? ActionName { get; init; }
        /// <summary>HTTP response status code.</summary>
        public int? StatusCode { get; init; }
        /// <summary>HTTP method (GET, POST, etc.).</summary>
        public string? HttpMethod { get; init; }
        /// <summary>Request path.</summary>
        public string? RequestPath { get; init; }
        /// <summary>Serialized action arguments (only when configured).</summary>
        public string? ActionArguments { get; init; }
    }

    /// <summary>
    /// Default <see cref="IAuditLogger"/> that writes to <see cref="ILogger"/>.
    /// Replace with a custom implementation for production audit trails.
    /// </summary>
    public class ConsoleAuditLogger : IAuditLogger
    {
        private readonly ILogger<ConsoleAuditLogger> _logger;

        public ConsoleAuditLogger(ILogger<ConsoleAuditLogger> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task LogAsync(AuditEvent e)
        {
            _logger.LogInformation(
                "[SecurityAudit] {Timestamp:O} | IP={IpAddress} | User={UserIdentity} | " +
                "Method={HttpMethod} | Path={RequestPath} | Action={ActionName} | Status={StatusCode}",
                e.Timestamp,
                e.IpAddress ?? "—",
                e.UserIdentity ?? "anonymous",
                e.HttpMethod ?? "—",
                e.RequestPath ?? "—",
                e.ActionName ?? "—",
                e.StatusCode?.ToString() ?? "—");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Action filter attribute that records a security audit event after every
    /// controller action execution. Resolves <see cref="IAuditLogger"/> from DI.
    ///
    /// Usage:
    /// <code>
    /// [SecurityAudit]
    /// public IActionResult SensitiveAction() { ... }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class SecurityAuditAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var executedContext = await next();

            try
            {
                var auditLogger = context.HttpContext.RequestServices
                    .GetService<IAuditLogger>();

                if (auditLogger == null) return;

                var options = context.HttpContext.RequestServices
                    .GetService<AuditOptions>() ?? new AuditOptions();

                var actionDescriptor = context.ActionDescriptor;
                var httpContext = context.HttpContext;

                string? args = null;
                if (options.LogActionArguments && context.ActionArguments.Count > 0)
                {
                    try
                    {
                        // Avoid leaking sensitive values: only capture argument names
                        args = string.Join(", ", context.ActionArguments.Keys);
                    }
                    catch { /* swallow — logging must never break the request */ }
                }

                var auditEvent = new AuditEvent
                {
                    Timestamp = DateTime.UtcNow,
                    IpAddress = options.LogIpAddress
                                        ? httpContext.Connection.RemoteIpAddress?.ToString()
                                        : null,
                    UserIdentity = options.LogUserIdentity
                                        ? httpContext.User?.Identity?.Name
                                        : null,
                    ActionName = options.LogActionName
                                        ? actionDescriptor.DisplayName
                                        : null,
                    StatusCode = options.LogStatusCode
                                        ? httpContext.Response.StatusCode
                                        : (int?)null,
                    HttpMethod = httpContext.Request.Method,
                    RequestPath = httpContext.Request.Path.ToString(),
                    ActionArguments = args,
                };

                await auditLogger.LogAsync(auditEvent);
            }
            catch
            {
                // Audit logging must NEVER propagate exceptions to the caller
            }
        }
    }

    /// <summary>
    /// Extension methods for registering security audit services.
    /// </summary>
    public static class SecurityAuditExtensions
    {
        /// <summary>
        /// Registers the default <see cref="ConsoleAuditLogger"/> and <see cref="AuditOptions"/>
        /// with default settings.
        /// </summary>
        public static IServiceCollection AddSecurityAudit(this IServiceCollection services)
            => services.AddSecurityAudit(_ => { });

        /// <summary>
        /// Registers the default <see cref="ConsoleAuditLogger"/> with configurable options.
        /// </summary>
        public static IServiceCollection AddSecurityAudit(
            this IServiceCollection services,
            Action<AuditOptions> configure)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            var opts = new AuditOptions();
            configure?.Invoke(opts);

            services.AddSingleton(opts);
            services.AddSingleton<IAuditLogger, ConsoleAuditLogger>();
            return services;
        }

        /// <summary>
        /// Registers a custom <see cref="IAuditLogger"/> implementation with configurable options.
        /// </summary>
        public static IServiceCollection AddSecurityAudit<TAuditLogger>(
            this IServiceCollection services,
            Action<AuditOptions>? configure = null)
            where TAuditLogger : class, IAuditLogger
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            var opts = new AuditOptions();
            configure?.Invoke(opts);

            services.AddSingleton(opts);
            services.AddSingleton<IAuditLogger, TAuditLogger>();
            return services;
        }
    }
}
