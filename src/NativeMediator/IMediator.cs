namespace NativeMediator;

/// <summary>
/// Defines a mediator to encapsulate request/response and publish notification patterns.
/// </summary>
public interface IMediator : ISender, IPublisher;

/// <summary>
/// Defines a sender for sending requests through the mediator pipeline.
/// </summary>
public interface ISender
{
    /// <summary>
    /// Sends a request to a single handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of response from the handler.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the send operation with the response.</returns>
    ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a stream of responses from a streaming request handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of each item in the stream.</typeparam>
    /// <param name="request">The streaming request to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of response items.</returns>
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines a publisher for publishing notifications to multiple handlers.
/// </summary>
public interface IPublisher
{
    /// <summary>
    /// Publishes a notification to all registered handlers.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the publish operation.</returns>
    ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
