using System.Runtime.CompilerServices;

namespace NativeMediator.Tests;

public record NumberStreamRequest(int Count) : IStreamRequest<int>;

public class NumberStreamHandler : IStreamRequestHandler<NumberStreamRequest, int>
{
    public async IAsyncEnumerable<int> Handle(
        NumberStreamRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var i = 1; i <= request.Count; i++)
        {
            await Task.Delay(10, cancellationToken);
            yield return i;
        }
    }
}
