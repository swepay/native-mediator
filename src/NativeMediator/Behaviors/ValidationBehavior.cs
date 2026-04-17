namespace NativeMediator.Behaviors;

/// <summary>
/// A pipeline behavior that validates requests before handling.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="validators">The validators to run.</param>
    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators ?? throw new ArgumentNullException(nameof(validators));
    }

    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<ValidationError>();

        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(request, cancellationToken);
            if (!result.IsValid)
            {
                errors.AddRange(result.Errors);
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return await next();
    }
}

/// <summary>
/// Defines a validator for a request.
/// </summary>
/// <typeparam name="TRequest">The type of request to validate.</typeparam>
public interface IValidator<in TRequest>
{
    /// <summary>
    /// Validates the specified request.
    /// </summary>
    /// <param name="request">The request to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The validation result.</returns>
    ValueTask<ValidationResult> ValidateAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets whether the validation was successful.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// A successful validation result.
    /// </summary>
    public static ValidationResult Success { get; } = new([]);

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public ValidationResult(IReadOnlyList<ValidationError> errors)
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    /// <summary>
    /// Creates a failed validation result with the specified errors.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <returns>A failed validation result.</returns>
    public static ValidationResult Failure(params ValidationError[] errors) => new(errors);
}

/// <summary>
/// Represents a validation error.
/// </summary>
/// <param name="PropertyName">The name of the property that failed validation.</param>
/// <param name="ErrorMessage">The error message.</param>
/// <param name="ErrorCode">An optional error code.</param>
public readonly record struct ValidationError(
    string PropertyName,
    string ErrorMessage,
    string? ErrorCode = null);

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public sealed class ValidationException : Exception
{
    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public ValidationException(IReadOnlyList<ValidationError> errors)
        : base(FormatMessage(errors))
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
    }

    private static string FormatMessage(IReadOnlyList<ValidationError> errors)
    {
        if (errors.Count == 0)
        {
            return "Validation failed.";
        }

        if (errors.Count == 1)
        {
            return $"Validation failed: {errors[0].ErrorMessage}";
        }

        return $"Validation failed with {errors.Count} errors. First error: {errors[0].ErrorMessage}";
    }
}
