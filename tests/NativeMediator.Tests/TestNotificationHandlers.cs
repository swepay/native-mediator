namespace NativeMediator.Tests;

public record TestNotification(string Message) : INotification;

public class TestNotificationHandler1 : INotificationHandler<TestNotification>
{
    public static List<string> ReceivedMessages { get; } = [];

    public ValueTask Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        ReceivedMessages.Add($"Handler1: {notification.Message}");
        return ValueTask.CompletedTask;
    }

    public static void Reset() => ReceivedMessages.Clear();
}

public class TestNotificationHandler2 : INotificationHandler<TestNotification>
{
    public static List<string> ReceivedMessages { get; } = [];

    public ValueTask Handle(TestNotification notification, CancellationToken cancellationToken = default)
    {
        ReceivedMessages.Add($"Handler2: {notification.Message}");
        return ValueTask.CompletedTask;
    }

    public static void Reset() => ReceivedMessages.Clear();
}
