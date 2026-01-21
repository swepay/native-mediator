namespace NativeMediator;

/// <summary>
/// Defines a pipeline behavior that can wrap request handling.
/// Useful for cross-cutting concerns like logging, validation, caching, etc.
/// </summary>
/// <typeparam name="TRequest">The type of request being handled.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>
    /// Pipeline handler. Perform any additional behavior and call the next delegate to continue the pipeline.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response from the next delegate or a short-circuited response.</returns>
    ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an async continuation for the next task to execute in the pipeline.
/// </summary>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
/// <returns>A task representing the async operation with the response.</returns>
public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>();
