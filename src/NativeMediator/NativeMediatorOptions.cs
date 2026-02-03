using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace NativeMediator;

/// <summary>
/// Configuration options for NativeMediator.
/// </summary>
public sealed class NativeMediatorOptions
{
    /// <summary>
    /// Gets or sets the lifetime of the mediator service. Default is Scoped.
    /// </summary>
    public ServiceLifetime MediatorLifetime { get; set; } = ServiceLifetime.Scoped;

    /// <summary>
    /// Gets or sets the default lifetime for handlers. Default is Scoped.
    /// </summary>
    public ServiceLifetime DefaultHandlerLifetime { get; set; } = ServiceLifetime.Scoped;

    /// <summary>
    /// Gets the list of assemblies to scan for handlers.
    /// Note: Assembly scanning is not AOT-compatible.
    /// </summary>
    public IList<Assembly> AssembliesToScan { get; } = [];

    /// <summary>
    /// Gets the list of handler registrations for AOT-compatible explicit registration.
    /// </summary>
    internal IList<HandlerRegistration> HandlerRegistrations { get; } = [];

    /// <summary>
    /// Gets the list of pipeline behavior registrations.
    /// </summary>
    internal IList<BehaviorRegistration> BehaviorRegistrations { get; } = [];
}

/// <summary>
/// Represents a handler registration for explicit AOT-compatible registration.
/// </summary>
internal sealed class HandlerRegistration
{
    public required Type ServiceType { get; init; }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public required Type ImplementationType { get; init; }

    /// <summary>
    /// Action to register the handler wrapper in the appropriate registry.
    /// </summary>
    public Action<RequestHandlerRegistry, StreamRequestHandlerRegistry>? RegisterWrapper { get; init; }

    /// <summary>
    /// Indicates if this is a notification handler (allows multiple handlers for same notification type).
    /// </summary>
    public bool IsNotificationHandler { get; init; }

    public ServiceLifetime Lifetime { get; init; } = ServiceLifetime.Scoped;
}

/// <summary>
/// Represents a pipeline behavior registration.
/// </summary>
internal sealed class BehaviorRegistration
{
    public required Type ServiceType { get; init; }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public required Type ImplementationType { get; init; }

    public ServiceLifetime Lifetime { get; init; } = ServiceLifetime.Scoped;
}
