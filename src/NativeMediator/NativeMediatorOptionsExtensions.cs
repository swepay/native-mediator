using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace NativeMediator;

/// <summary>
/// Extension methods for configuring handlers in NativeMediatorOptions.
/// Provides AOT-friendly explicit handler registration.
/// </summary>
public static class NativeMediatorOptionsExtensions
{
    /// <summary>
    /// Registers a request handler explicitly for AOT compatibility.
    /// </summary>
    /// <typeparam name="TRequest">The type of request.</typeparam>
    /// <typeparam name="TResponse">The type of response.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <param name="options">The options to configure.</param>
    /// <param name="lifetime">The service lifetime. Default is Scoped.</param>
    /// <returns>The options for chaining.</returns>
    public static NativeMediatorOptions AddHandler<TRequest, TResponse, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this NativeMediatorOptions options,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TRequest : IRequest<TResponse>
        where THandler : class, IRequestHandler<TRequest, TResponse>
    {
        options.HandlerRegistrations.Add(new HandlerRegistration
        {
            ServiceType = typeof(IRequestHandler<TRequest, TResponse>),
            ImplementationType = typeof(THandler),
            WrapperServiceType = typeof(IRequestHandlerWrapper<TResponse>),
            WrapperImplementationType = typeof(RequestHandlerWrapper<TRequest, TResponse>),
            Lifetime = lifetime
        });
        return options;
    }

    /// <summary>
    /// Registers a request handler without response explicitly for AOT compatibility.
    /// </summary>
    /// <typeparam name="TRequest">The type of request.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <param name="options">The options to configure.</param>
    /// <param name="lifetime">The service lifetime. Default is Scoped.</param>
    /// <returns>The options for chaining.</returns>
    public static NativeMediatorOptions AddHandler<TRequest, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this NativeMediatorOptions options,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TRequest : IRequest<Unit>
        where THandler : class, IRequestHandler<TRequest, Unit>
    {
        return options.AddHandler<TRequest, Unit, THandler>(lifetime);
    }

    /// <summary>
    /// Registers a notification handler explicitly for AOT compatibility.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <param name="options">The options to configure.</param>
    /// <param name="lifetime">The service lifetime. Default is Scoped.</param>
    /// <returns>The options for chaining.</returns>
    public static NativeMediatorOptions AddNotificationHandler<TNotification, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this NativeMediatorOptions options,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TNotification : INotification
        where THandler : class, INotificationHandler<TNotification>
    {
        options.HandlerRegistrations.Add(new HandlerRegistration
        {
            ServiceType = typeof(INotificationHandler<TNotification>),
            ImplementationType = typeof(THandler),
            IsNotificationHandler = true,
            Lifetime = lifetime
        });
        return options;
    }

    /// <summary>
    /// Registers a stream request handler explicitly for AOT compatibility.
    /// </summary>
    /// <typeparam name="TRequest">The type of stream request.</typeparam>
    /// <typeparam name="TResponse">The type of each item in the stream.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <param name="options">The options to configure.</param>
    /// <param name="lifetime">The service lifetime. Default is Scoped.</param>
    /// <returns>The options for chaining.</returns>
    public static NativeMediatorOptions AddStreamHandler<TRequest, TResponse, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>(
        this NativeMediatorOptions options,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TRequest : IStreamRequest<TResponse>
        where THandler : class, IStreamRequestHandler<TRequest, TResponse>
    {
        options.HandlerRegistrations.Add(new HandlerRegistration
        {
            ServiceType = typeof(IStreamRequestHandler<TRequest, TResponse>),
            ImplementationType = typeof(THandler),
            WrapperServiceType = typeof(IStreamRequestHandlerWrapper<TResponse>),
            WrapperImplementationType = typeof(StreamRequestHandlerWrapper<TRequest, TResponse>),
            Lifetime = lifetime
        });
        return options;
    }

    /// <summary>
    /// Registers a pipeline behavior for a specific request/response type.
    /// </summary>
    /// <typeparam name="TRequest">The type of request.</typeparam>
    /// <typeparam name="TResponse">The type of response.</typeparam>
    /// <typeparam name="TBehavior">The pipeline behavior type.</typeparam>
    /// <param name="options">The options to configure.</param>
    /// <param name="lifetime">The service lifetime. Default is Scoped.</param>
    /// <returns>The options for chaining.</returns>
    public static NativeMediatorOptions AddBehavior<TRequest, TResponse, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TBehavior>(
        this NativeMediatorOptions options,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TRequest : IRequest<TResponse>
        where TBehavior : class, IPipelineBehavior<TRequest, TResponse>
    {
        options.BehaviorRegistrations.Add(new BehaviorRegistration
        {
            ServiceType = typeof(IPipelineBehavior<TRequest, TResponse>),
            ImplementationType = typeof(TBehavior),
            Lifetime = lifetime
        });
        return options;
    }

    /// <summary>
    /// Adds an assembly to scan for handlers.
    /// Note: This uses reflection and is NOT AOT compatible.
    /// </summary>
    /// <param name="options">The options to configure.</param>
    /// <param name="assembly">The assembly to scan.</param>
    /// <returns>The options for chaining.</returns>
    [RequiresUnreferencedCode("Assembly scanning uses reflection and is not AOT-compatible.")]
    public static NativeMediatorOptions AddHandlersFromAssembly(
        this NativeMediatorOptions options,
        System.Reflection.Assembly assembly)
    {
        options.AssembliesToScan.Add(assembly);
        return options;
    }

    /// <summary>
    /// Adds the assembly containing the specified type to scan for handlers.
    /// Note: This uses reflection and is NOT AOT compatible.
    /// </summary>
    /// <typeparam name="T">A type from the assembly to scan.</typeparam>
    /// <param name="options">The options to configure.</param>
    /// <returns>The options for chaining.</returns>
    [RequiresUnreferencedCode("Assembly scanning uses reflection and is not AOT-compatible.")]
    public static NativeMediatorOptions AddHandlersFromAssemblyContaining<T>(
        this NativeMediatorOptions options)
    {
        return options.AddHandlersFromAssembly(typeof(T).Assembly);
    }
}
