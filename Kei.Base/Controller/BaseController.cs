using Kei.Base.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kei.Base.Controller
{
    public abstract class BaseController : ControllerBase
    {
        protected IActionResult Success() => Ok(OperationResult.Ok());
        protected IActionResult Success(string message) => Ok(OperationResult.Ok(message));
        protected IActionResult Success<T>(T data, string? message = null) =>
            Ok(OperationResult<T>.Ok(data, message));
        protected IActionResult Error(string message) =>
            BadRequest(OperationResult.Fail(message));
        protected IActionResult Error<T>(string message) =>
            BadRequest(OperationResult<T>.Fail(message));

        // ─── Security─Hardened Response Helpers ─────────────────────────────

        /// <summary>
        /// Returns HTTP 404 without leaking the resource type or ID.
        /// Prevents resource enumeration by keeping the error response generic.
        /// </summary>
        protected IActionResult NotFoundSafe()
            => NotFound(OperationResult.Fail("The requested resource was not found."));

        /// <summary>
        /// Returns HTTP 403 with minimal disclosure.
        /// Use instead of <see cref="ControllerBase.Forbid()" /> to avoid
        /// leaking authentication challenge details.
        /// </summary>
        protected IActionResult ForbidSafe(string reason = "Access denied.")
            => StatusCode(403, OperationResult.Fail(reason));

        /// <summary>
        /// Returns HTTP 401 with a generic message.
        /// Avoids leaking authentication scheme details.
        /// </summary>
        protected IActionResult UnauthorizedSafe()
            => StatusCode(401, OperationResult.Fail("Authentication is required."));

        /// <summary>
        /// Returns HTTP 422 (Unprocessable Entity) with sanitized model-state errors.
        /// Strips stack traces and internal details from validation messages.
        /// </summary>
        protected IActionResult ValidationError(ModelStateDictionary modelState)
        {
            var errors = modelState
                .Where(kvp => kvp.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            return StatusCode(422, new { Success = false, Message = "Validation failed.", Errors = errors });
        }
    }
}
