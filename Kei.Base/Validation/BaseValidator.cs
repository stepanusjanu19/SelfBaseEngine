using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Kei.Base.Models;

namespace Kei.Base.Validation
{
    /// <summary>
    /// Base validator utilizing FluentValidation, offering standardized validation methods 
    /// for Data Transfer Objects (DTOs) and Data Access Objects (DAOs).
    /// </summary>
    /// <typeparam name="T">The type of object being validated.</typeparam>
    public abstract class BaseValidator<T> : AbstractValidator<T>
    {
        /// <summary>
        /// Validates the object and wraps the result in a standardized OperationResult.
        /// If validation fails, it aggregates the error messages.
        /// </summary>
        /// <param name="instance">The object to validate.</param>
        /// <returns>An OperationResult containing the validation outcome.</returns>
        public OperationResult<T> ValidateToResult(T instance)
        {
            if (instance == null)
            {
                return OperationResult<T>.Fail("Instance to validate cannot be null.");
            }

            ValidationResult validationResult = Validate(instance);

            if (validationResult.IsValid)
            {
                return OperationResult<T>.Ok(instance, "Validation successful.");
            }

            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            var errorMessage = string.Join(" | ", errors);

            return OperationResult<T>.Fail($"Validation failed: {errorMessage}");
        }

        /// <summary>
        /// Asynchronously validates the object and wraps the result in a standardized OperationResult.
        /// </summary>
        /// <param name="instance">The object to validate.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An OperationResult containing the validation outcome.</returns>
        public async Task<OperationResult<T>> ValidateToResultAsync(T instance, CancellationToken cancellationToken = default)
        {
            if (instance == null)
            {
                return OperationResult<T>.Fail("Instance to validate cannot be null.");
            }

            ValidationResult validationResult = await ValidateAsync(instance, cancellationToken);

            if (validationResult.IsValid)
            {
                return OperationResult<T>.Ok(instance, "Validation successful.");
            }

            var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
            var errorMessage = string.Join(" | ", errors);

            return OperationResult<T>.Fail($"Validation failed: {errorMessage}");
        }
    }
}
