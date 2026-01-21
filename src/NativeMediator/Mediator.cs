using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;

namespace NativeMediator;

/// <summary>
/// AOT-optimized mediator implementation that uses strongly-typed handler wrappers.
/// This implementation avoids reflection by requiring explicit handler registration.
/// </summary>
public sealed class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mediator"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
    public Mediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Try to resolve the wrapper for AOT-friendly invocation
        var wrapper = _serviceProvider.GetService<IRequestHandlerWrapper<TResponse>>();
        if (wrapper is not null)
        {
            return wrapper.Handle(request, _serviceProvider, cancellationToken);
        }

        // Fallback for direct handler resolution (when request type is known at compile time)
        throw new InvalidOperationException(
            $"No handler wrapper registered for response type '{typeof(TResponse).FullName}'. " +
            $"Ensure you have registered handlers using AddHandler<TRequest, TResponse, THandler>().");
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = _serviceProvider.GetService<IStreamRequestHandlerWrapper<TResponse>>();
        if (wrapper is null)
        {
            throw new InvalidOperationException(
                $"No stream handler wrapper registered for response type '{typeof(TResponse).FullName}'. " +
                $"Ensure you have registered handlers using AddStreamHandler<TRequest, TResponse, THandler>().");
        }

        await foreach (var item in wrapper.Handle(request, _serviceProvider, cancellationToken))
        {
            yield return item;
        }
    }

    /// <inheritdoc/>
    public async ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        var handlers = _serviceProvider.GetServices<INotificationHandler<TNotification>>();

        foreach (var handler in handlers)
        {
            await handler.Handle(notification, cancellationToken);
        }
    }
}

/// <summary>
/// Internal interface for AOT-friendly request handler invocation.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestHandlerWrapper<TResponse>
{
    /// <summary>
    /// Handles the request and returns the response.
    /// </summary>
    ValueTask<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Internal interface for AOT-friendly stream request handler invocation.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IStreamRequestHandlerWrapper<out TResponse>
{
    /// <summary>
    /// Handles the stream request and returns the stream.
    /// </summary>
    IAsyncEnumerable<TResponse> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Concrete wrapper for a specific request/response pair.
/// </summary>
internal sealed class RequestHandlerWrapper<TRequest, TResponse> : IRequestHandlerWrapper<TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (request is not TRequest typedRequest)
        {
            throw new InvalidOperationException($"Request is not of expected type '{typeof(TRequest).FullName}'.");
        }

        var handler = serviceProvider.GetService<IRequestHandler<TRequest, TResponse>>();
        if (handler is null)
        {
            throw new InvalidOperationException(
                $"No handler registered for request type '{typeof(TRequest).FullName}'. " +
                $"Ensure you have registered a handler implementing IRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}>.");
        }

        // Get pipeline behaviors
        var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse().ToList();

        // Build the pipeline
        RequestHandlerDelegate<TResponse> pipeline = () => handler.Handle(typedRequest, cancellationToken);

        foreach (var behavior in behaviors)
        {
            var currentPipeline = pipeline;
            var currentBehavior = behavior;
            pipeline = () => currentBehavior.Handle(typedRequest, currentPipeline, cancellationToken);
        }

        return await pipeline();
    }
}

/// <summary>
/// Concrete wrapper for a specific stream request/response pair.
/// </summary>
internal sealed class StreamRequestHandlerWrapper<TRequest, TResponse> : IStreamRequestHandlerWrapper<TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public async IAsyncEnumerable<TResponse> Handle(
        object request,
        IServiceProvider serviceProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request is not TRequest typedRequest)
        {
            throw new InvalidOperationException($"Request is not of expected type '{typeof(TRequest).FullName}'.");
        }

        var handler = serviceProvider.GetService<IStreamRequestHandler<TRequest, TResponse>>();
        if (handler is null)
        {
            throw new InvalidOperationException(
                $"No stream handler registered for request type '{typeof(TRequest).FullName}'. " +
                $"Ensure you have registered a handler implementing IStreamRequestHandler<{typeof(TRequest).Name}, {typeof(TResponse).Name}>.");
        }

        await foreach (var item in handler.Handle(typedRequest, cancellationToken))
        {
            yield return item;
        }
    }
}
