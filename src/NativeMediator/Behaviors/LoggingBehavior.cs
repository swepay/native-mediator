using System.Diagnostics;

namespace NativeMediator.Behaviors;

/// <summary>
/// A pipeline behavior that logs the execution time of requests.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly Action<string> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="logger">The logging action.</param>
    public LoggingBehavior(Action<string> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Initializes a new instance with console logging.
    /// </summary>
    public LoggingBehavior() : this(Console.WriteLine) { }

    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        var requestName = typeof(TRequest).Name;

        _logger($"[NativeMediator] Handling {requestName}");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            stopwatch.Stop();

            _logger($"[NativeMediator] Handled {requestName} in {stopwatch.ElapsedMilliseconds}ms");

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger($"[NativeMediator] Error handling {requestName} after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
            throw;
        }
    }
}
