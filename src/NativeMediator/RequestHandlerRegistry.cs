using System.Collections.Concurrent;

namespace NativeMediator;

/// <summary>
/// Registry that holds wrapper factories indexed by request type.
/// This allows multiple request types to share the same response type.
/// </summary>
public sealed class RequestHandlerRegistry
{
    private readonly ConcurrentDictionary<Type, object> _wrappers = new();

    /// <summary>
    /// Registers a wrapper for a specific request type.
    /// Creates a new wrapper instance for the specified request/response types.
    /// </summary>
    public void Register<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>
    {
        var wrapper = new RequestHandlerWrapper<TRequest, TResponse>();
        _wrappers[typeof(TRequest)] = wrapper;
    }

    /// <summary>
    /// Gets the wrapper for a specific request type.
    /// </summary>
    public IRequestHandlerWrapper<TRequest, TResponse>? GetWrapper<TRequest, TResponse>()
        where TRequest : IRequest<TResponse>
    {
        if (_wrappers.TryGetValue(typeof(TRequest), out var wrapper))
        {
            return wrapper as IRequestHandlerWrapper<TRequest, TResponse>;
        }
        return null;
    }

    /// <summary>
    /// Gets the wrapper for a request instance.
    /// </summary>
    public object? GetWrapper(Type requestType)
    {
        _wrappers.TryGetValue(requestType, out var wrapper);
        return wrapper;
    }
}

/// <summary>
/// Registry for stream request handler wrappers.
/// </summary>
public sealed class StreamRequestHandlerRegistry
{
    private readonly ConcurrentDictionary<Type, object> _wrappers = new();

    /// <summary>
    /// Registers a wrapper for a specific stream request type.
    /// Creates a new wrapper instance for the specified request/response types.
    /// </summary>
    public void Register<TRequest, TResponse>()
        where TRequest : IStreamRequest<TResponse>
    {
        var wrapper = new StreamRequestHandlerWrapper<TRequest, TResponse>();
        _wrappers[typeof(TRequest)] = wrapper;
    }

    /// <summary>
    /// Gets the wrapper for a request instance.
    /// </summary>
    public object? GetWrapper(Type requestType)
    {
        _wrappers.TryGetValue(requestType, out var wrapper);
        return wrapper;
    }
}
