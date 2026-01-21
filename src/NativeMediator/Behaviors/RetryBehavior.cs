namespace NativeMediator.Behaviors;

/// <summary>
/// A pipeline behavior that retries failed requests.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public sealed class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly int _maxRetries;
    private readonly TimeSpan _delay;
    private readonly Func<Exception, bool>? _shouldRetry;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retry attempts.</param>
    /// <param name="delay">The delay between retries.</param>
    /// <param name="shouldRetry">Optional predicate to determine if an exception should trigger a retry.</param>
    public RetryBehavior(int maxRetries = 3, TimeSpan? delay = null, Func<Exception, bool>? shouldRetry = null)
    {
        _maxRetries = maxRetries > 0 ? maxRetries : throw new ArgumentOutOfRangeException(nameof(maxRetries));
        _delay = delay ?? TimeSpan.FromMilliseconds(100);
        _shouldRetry = shouldRetry;
    }

    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;

        while (true)
        {
            try
            {
                return await next();
            }
            catch (Exception ex) when (attempt < _maxRetries && ShouldRetry(ex))
            {
                attempt++;
                await Task.Delay(_delay, cancellationToken);
            }
        }
    }

    private bool ShouldRetry(Exception ex)
    {
        if (_shouldRetry is not null)
        {
            return _shouldRetry(ex);
        }

        // By default, retry on transient exceptions
        return ex is TimeoutException or HttpRequestException;
    }
}
