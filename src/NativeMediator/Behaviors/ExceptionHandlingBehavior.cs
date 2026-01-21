namespace NativeMediator.Behaviors;

/// <summary>
/// A pipeline behavior that wraps request handling in a try-catch and handles exceptions.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public sealed class ExceptionHandlingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly Func<TRequest, Exception, CancellationToken, ValueTask<TResponse>>? _exceptionHandler;
    private readonly Action<TRequest, Exception>? _onException;

    /// <summary>
    /// Initializes a new instance with an exception handler that can return a fallback response.
    /// </summary>
    /// <param name="exceptionHandler">The exception handler.</param>
    public ExceptionHandlingBehavior(Func<TRequest, Exception, CancellationToken, ValueTask<TResponse>> exceptionHandler)
    {
        _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
    }

    /// <summary>
    /// Initializes a new instance with a logging callback that re-throws the exception.
    /// </summary>
    /// <param name="onException">The callback to invoke when an exception occurs.</param>
    public ExceptionHandlingBehavior(Action<TRequest, Exception> onException)
    {
        _onException = onException ?? throw new ArgumentNullException(nameof(onException));
    }

    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await next();
        }
        catch (Exception ex)
        {
            if (_exceptionHandler is not null)
            {
                return await _exceptionHandler(request, ex, cancellationToken);
            }

            _onException?.Invoke(request, ex);
            throw;
        }
    }
}
