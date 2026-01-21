namespace NativeMediator.Tests;

public record PingRequest(string Message) : IRequest<PingResponse>;
public record PingResponse(string Reply);

public class PingHandler : IRequestHandler<PingRequest, PingResponse>
{
    public ValueTask<PingResponse> Handle(PingRequest request, CancellationToken cancellationToken = default)
    {
        return new ValueTask<PingResponse>(new PingResponse($"Pong: {request.Message}"));
    }
}

public record VoidRequest(string Data) : IRequest;

public class VoidHandler : IRequestHandler<VoidRequest>
{
    public static int CallCount { get; private set; }

    public ValueTask<Unit> Handle(VoidRequest request, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Unit.ValueTask;
    }

    public static void Reset() => CallCount = 0;
}
