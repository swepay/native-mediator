using Microsoft.Extensions.DependencyInjection;

namespace NativeMediator.Tests;

public class MediatorTests
{
    [Fact]
    public async Task Send_WithValidHandler_ReturnsResponse()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddNativeMediator(options =>
        {
            options.AddHandler<PingRequest, PingResponse, PingHandler>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var response = await mediator.Send(new PingRequest("Hello"));

        // Assert
        Assert.Equal("Pong: Hello", response.Reply);
    }

    [Fact]
    public async Task Send_WithVoidHandler_ReturnsUnit()
    {
        // Arrange
        VoidHandler.Reset();
        var services = new ServiceCollection();
        services.AddNativeMediator(options =>
        {
            options.AddHandler<VoidRequest, VoidHandler>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new VoidRequest("Test"));

        // Assert
        Assert.Equal(Unit.Value, result);
        Assert.Equal(1, VoidHandler.CallCount);
    }

    [Fact]
    public async Task Send_WithNoHandler_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddNativeMediator();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Send(new PingRequest("Hello")).AsTask());
    }

    [Fact]
    public async Task Send_WithNullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddNativeMediator();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => mediator.Send<PingResponse>(null!).AsTask());
    }
}

public class NotificationTests
{
    [Fact]
    public async Task Publish_WithMultipleHandlers_AllHandlersReceiveNotification()
    {
        // Arrange
        TestNotificationHandler1.Reset();
        TestNotificationHandler2.Reset();

        var services = new ServiceCollection();
        services.AddNativeMediator(options =>
        {
            options.AddNotificationHandler<TestNotification, TestNotificationHandler1>();
            options.AddNotificationHandler<TestNotification, TestNotificationHandler2>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        await mediator.Publish(new TestNotification("Hello World"));

        // Assert
        Assert.Single(TestNotificationHandler1.ReceivedMessages);
        Assert.Single(TestNotificationHandler2.ReceivedMessages);
        Assert.Contains("Handler1: Hello World", TestNotificationHandler1.ReceivedMessages);
        Assert.Contains("Handler2: Hello World", TestNotificationHandler2.ReceivedMessages);
    }

    [Fact]
    public async Task Publish_WithNoHandlers_DoesNotThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddNativeMediator();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act & Assert (should not throw)
        await mediator.Publish(new TestNotification("Hello"));
    }
}

public class StreamTests
{
    [Fact]
    public async Task CreateStream_WithValidHandler_ReturnsStream()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddNativeMediator(options =>
        {
            options.AddStreamHandler<NumberStreamRequest, int, NumberStreamHandler>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var results = new List<int>();
        await foreach (var item in mediator.CreateStream(new NumberStreamRequest(5)))
        {
            results.Add(item);
        }

        // Assert
        Assert.Equal([1, 2, 3, 4, 5], results);
    }

    [Fact]
    public async Task CreateStream_WithNoHandler_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddNativeMediator();

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in mediator.CreateStream(new NumberStreamRequest(5)))
            {
            }
        });
    }
}

public class PipelineBehaviorTests
{
    [Fact]
    public async Task Send_WithPipelineBehavior_ExecutesBehaviorAroundHandler()
    {
        // Arrange
        TestPipelineBehavior<PingRequest, PingResponse>.Reset();

        var services = new ServiceCollection();
        services.AddNativeMediator(options =>
        {
            options.AddHandler<PingRequest, PingResponse, PingHandler>();
            options.AddBehavior<PingRequest, PingResponse, TestPipelineBehavior<PingRequest, PingResponse>>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var response = await mediator.Send(new PingRequest("Test"));

        // Assert
        Assert.Equal("Pong: Test", response.Reply);
        Assert.Equal(2, TestPipelineBehavior<PingRequest, PingResponse>.ExecutionOrder.Count);
        Assert.Equal("Before: PingRequest", TestPipelineBehavior<PingRequest, PingResponse>.ExecutionOrder[0]);
        Assert.Equal("After: PingRequest", TestPipelineBehavior<PingRequest, PingResponse>.ExecutionOrder[1]);
    }
}

public class UnitTests
{
    [Fact]
    public void Unit_Value_IsSingleton()
    {
        Assert.Equal(Unit.Value, Unit.Value);
        Assert.True(Unit.Value == new Unit());
    }

    [Fact]
    public void Unit_Equals_AlwaysReturnsTrue()
    {
        var unit1 = Unit.Value;
        var unit2 = new Unit();

        Assert.True(unit1.Equals(unit2));
        Assert.True(unit1 == unit2);
        Assert.False(unit1 != unit2);
    }

    [Fact]
    public void Unit_GetHashCode_ReturnsZero()
    {
        Assert.Equal(0, Unit.Value.GetHashCode());
    }

    [Fact]
    public void Unit_ToString_ReturnsEmptyTuple()
    {
        Assert.Equal("()", Unit.Value.ToString());
    }

    [Fact]
    public void Unit_CompareTo_ReturnsZero()
    {
        Assert.Equal(0, Unit.Value.CompareTo(Unit.Value));
        Assert.Equal(0, ((IComparable)Unit.Value).CompareTo(Unit.Value));
    }
}

public class ServiceRegistrationTests
{
    [Fact]
    public void AddNativeMediator_RegistersIMediator()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNativeMediator();
        var provider = services.BuildServiceProvider();

        // Assert
        var mediator = provider.GetService<IMediator>();
        Assert.NotNull(mediator);
    }

    [Fact]
    public void AddNativeMediator_RegistersISender()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNativeMediator();
        var provider = services.BuildServiceProvider();

        // Assert
        var sender = provider.GetService<ISender>();
        Assert.NotNull(sender);
    }

    [Fact]
    public void AddNativeMediator_RegistersIPublisher()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNativeMediator();
        var provider = services.BuildServiceProvider();

        // Assert
        var publisher = provider.GetService<IPublisher>();
        Assert.NotNull(publisher);
    }

    [Fact]
    public void AddNativeMediator_WithSingletonLifetime_RegistersAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddNativeMediator(options =>
        {
            options.MediatorLifetime = ServiceLifetime.Singleton;
        });
        var provider = services.BuildServiceProvider();

        // Assert
        var mediator1 = provider.GetService<IMediator>();
        var mediator2 = provider.GetService<IMediator>();
        Assert.Same(mediator1, mediator2);
    }
}

