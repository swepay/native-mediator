namespace NativeMediator.Tests;

public class TestPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public static List<string> ExecutionOrder { get; } = [];

    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        ExecutionOrder.Add($"Before: {typeof(TRequest).Name}");
        var response = await next();
        ExecutionOrder.Add($"After: {typeof(TRequest).Name}");
        return response;
    }

    public static void Reset() => ExecutionOrder.Clear();
}

public class TestPipelineBehavior2<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public static List<string> ExecutionOrder { get; } = [];

    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        ExecutionOrder.Add($"Before2: {typeof(TRequest).Name}");
        var response = await next();
        ExecutionOrder.Add($"After2: {typeof(TRequest).Name}");
        return response;
    }

    public static void Reset() => ExecutionOrder.Clear();
}
