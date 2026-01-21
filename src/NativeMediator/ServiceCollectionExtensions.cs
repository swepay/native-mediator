using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace NativeMediator;

/// <summary>
/// Extension methods for configuring NativeMediator services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds NativeMediator services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">The action to configure NativeMediator options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNativeMediator(
        this IServiceCollection services,
        Action<NativeMediatorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new NativeMediatorOptions();
        configure(options);

        // Register the mediator
        var mediatorDescriptor = options.MediatorLifetime switch
        {
            ServiceLifetime.Singleton => ServiceDescriptor.Singleton<IMediator, Mediator>(),
            ServiceLifetime.Scoped => ServiceDescriptor.Scoped<IMediator, Mediator>(),
            ServiceLifetime.Transient => ServiceDescriptor.Transient<IMediator, Mediator>(),
            _ => throw new ArgumentOutOfRangeException(nameof(options.MediatorLifetime))
        };
        services.TryAdd(mediatorDescriptor);

        // Also register ISender and IPublisher
        services.TryAdd(ServiceDescriptor.Describe(
            typeof(ISender),
            sp => sp.GetRequiredService<IMediator>(),
            options.MediatorLifetime));

        services.TryAdd(ServiceDescriptor.Describe(
            typeof(IPublisher),
            sp => sp.GetRequiredService<IMediator>(),
            options.MediatorLifetime));

        // Register explicitly added handlers and their wrappers (AOT-friendly)
        foreach (var registration in options.HandlerRegistrations)
        {
            var descriptor = ServiceDescriptor.Describe(
                registration.ServiceType,
                registration.ImplementationType,
                registration.Lifetime);

            // Use Add for notification handlers (multiple handlers allowed), TryAdd for others
            if (registration.IsNotificationHandler)
            {
                services.Add(descriptor);
            }
            else
            {
                services.TryAdd(descriptor);
            }

            // Register wrapper if it's a request handler
            if (registration.WrapperServiceType is not null && registration.WrapperImplementationType is not null)
            {
                var wrapperDescriptor = ServiceDescriptor.Describe(
                    registration.WrapperServiceType,
                    registration.WrapperImplementationType,
                    registration.Lifetime);
                services.TryAdd(wrapperDescriptor);
            }
        }

        // Register pipeline behaviors
        foreach (var behaviorReg in options.BehaviorRegistrations)
        {
            services.Add(ServiceDescriptor.Describe(
                behaviorReg.ServiceType,
                behaviorReg.ImplementationType,
                behaviorReg.Lifetime));
        }

        // Scan assemblies for handlers (not AOT-friendly, but useful for JIT scenarios)
#pragma warning disable IL2026 // Assembly scanning is opt-in and documented as not AOT-compatible
        foreach (var assembly in options.AssembliesToScan)
        {
            RegisterHandlersFromAssembly(services, assembly, options.DefaultHandlerLifetime);
        }
#pragma warning restore IL2026

        return services;
    }

    /// <summary>
    /// Adds NativeMediator services with default options.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNativeMediator(this IServiceCollection services)
    {
        return services.AddNativeMediator(_ => { });
    }

    [RequiresUnreferencedCode("Assembly scanning uses reflection and is not AOT-compatible.")]
    private static void RegisterHandlersFromAssembly(
        IServiceCollection services,
        System.Reflection.Assembly assembly,
        ServiceLifetime lifetime)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .Where(t => t.GetInterfaces().Any(IsHandlerInterface));

        foreach (var handlerType in handlerTypes)
        {
            var interfaces = handlerType.GetInterfaces().Where(IsHandlerInterface);
            foreach (var @interface in interfaces)
            {
                services.TryAdd(ServiceDescriptor.Describe(@interface, handlerType, lifetime));
            }
        }
    }

    private static bool IsHandlerInterface(Type type)
    {
        if (!type.IsGenericType) return false;

        var genericTypeDef = type.GetGenericTypeDefinition();
        return genericTypeDef == typeof(IRequestHandler<,>) ||
               genericTypeDef == typeof(INotificationHandler<>) ||
               genericTypeDef == typeof(IStreamRequestHandler<,>);
    }
}
