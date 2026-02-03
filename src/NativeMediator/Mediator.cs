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
    private readonly RequestHandlerRegistry _handlerRegistry;
    private readonly StreamRequestHandlerRegistry _streamHandlerRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mediator"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve handlers.</param>
    /// <param name="handlerRegistry">The registry containing handler wrappers.</param>
    /// <param name="streamHandlerRegistry">The registry containing stream handler wrappers.</param>
    public Mediator(
        IServiceProvider serviceProvider,
        RequestHandlerRegistry handlerRegistry,
        StreamRequestHandlerRegistry streamHandlerRegistry)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _handlerRegistry = handlerRegistry ?? throw new ArgumentNullException(nameof(handlerRegistry));
        _streamHandlerRegistry = streamHandlerRegistry ?? throw new ArgumentNullException(nameof(streamHandlerRegistry));
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var wrapper = _handlerRegistry.GetWrapper(requestType);

        if (wrapper is null)
        {
            throw new InvalidOperationException(
                $"No handler registered for request type '{requestType.FullName}'. " +
                $"Ensure you have registered a handler using AddHandler<TRequest, TResponse, THandler>().");
        }

        // Use dynamic dispatch to invoke the correct wrapper
        return InvokeWrapper<TResponse>(wrapper, request, cancellationToken);
    }

    private ValueTask<TResponse> InvokeWrapper<TResponse>(object wrapper, IRequest<TResponse> request, CancellationToken cancellationToken)
    {
        // The wrapper implements IRequestHandlerWrapperBase which has a non-generic Handle method
        if (wrapper is IRequestHandlerWrapperBase<TResponse> typedWrapper)
        {
            return typedWrapper.Handle(request, _serviceProvider, cancellationToken);
        }

        throw new InvalidOperationException(
            $"Wrapper for request type '{request.GetType().FullName}' does not implement the expected interface.");
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var wrapper = _streamHandlerRegistry.GetWrapper(requestType);

        if (wrapper is null)
        {
            throw new InvalidOperationException(
                $"No stream handler registered for request type '{requestType.FullName}'. " +
                $"Ensure you have registered a handler using AddStreamHandler<TRequest, TResponse, THandler>().");
        }

        if (wrapper is IStreamRequestHandlerWrapperBase<TResponse> typedWrapper)
        {
            await foreach (var item in typedWrapper.Handle(request, _serviceProvider, cancellationToken))
            {
                yield return item;
            }
        }
        else
        {
            throw new InvalidOperationException(
                $"Stream wrapper for request type '{requestType.FullName}' does not implement the expected interface.");
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
/// Base interface for request handler wrappers that can handle requests with a specific response type.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestHandlerWrapperBase<TResponse>
{
    /// <summary>
    /// Handles the request and returns the response.
    /// </summary>
    ValueTask<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Interface for AOT-friendly request handler invocation with specific request type.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestHandlerWrapper<TRequest, TResponse> : IRequestHandlerWrapperBase<TResponse>
    where TRequest : IRequest<TResponse>
{
}

/// <summary>
/// Base interface for stream request handler wrappers.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IStreamRequestHandlerWrapperBase<out TResponse>
{
    /// <summary>
    /// Handles the stream request and returns the stream.
    /// </summary>
    IAsyncEnumerable<TResponse> Handle(object request, IServiceProvider serviceProvider, CancellationToken cancellationToken);
}

/// <summary>
/// Interface for AOT-friendly stream request handler invocation.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IStreamRequestHandlerWrapper<TRequest, out TResponse> : IStreamRequestHandlerWrapperBase<TResponse>
    where TRequest : IStreamRequest<TResponse>
{
}

/// <summary>
/// Concrete wrapper for a specific request/response pair.
/// </summary>
internal sealed class RequestHandlerWrapper<TRequest, TResponse> : IRequestHandlerWrapper<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async ValueTask<TResponse> Handle(IRequest<TResponse> request, IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        if (request is not TRequest typedRequest)
        {
            throw new InvalidOperationException(
                $"Request is not of expected type '{typeof(TRequest).FullName}'. " +
                $"Received type: '{request.GetType().FullName}'.");
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
internal sealed class StreamRequestHandlerWrapper<TRequest, TResponse> : IStreamRequestHandlerWrapper<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    public async IAsyncEnumerable<TResponse> Handle(
        object request,
        IServiceProvider serviceProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request is not TRequest typedRequest)
        {
            throw new InvalidOperationException(
                $"Request is not of expected type '{typeof(TRequest).FullName}'. " +
                $"Received type: '{request.GetType().FullName}'.");
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
