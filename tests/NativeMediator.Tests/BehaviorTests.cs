using Microsoft.Extensions.DependencyInjection;
using NativeMediator.Behaviors;

namespace NativeMediator.Tests;

public class LoggingBehaviorTests
{
    [Fact]
    public async Task LoggingBehavior_LogsBeforeAndAfterHandling()
    {
        // Arrange
        var logs = new List<string>();
        var behavior = new LoggingBehavior<PingRequest, PingResponse>(logs.Add);
        var handler = new PingHandler();

        RequestHandlerDelegate<PingResponse> next = () => handler.Handle(new PingRequest("Test"));

        // Act
        var response = await behavior.Handle(new PingRequest("Test"), next);

        // Assert
        Assert.Equal("Pong: Test", response.Reply);
        Assert.Equal(2, logs.Count);
        Assert.Contains("Handling PingRequest", logs[0]);
        Assert.Contains("Handled PingRequest", logs[1]);
    }

    [Fact]
    public async Task LoggingBehavior_LogsExceptionWhenHandlerThrows()
    {
        // Arrange
        var logs = new List<string>();
        var behavior = new LoggingBehavior<PingRequest, PingResponse>(logs.Add);

        RequestHandlerDelegate<PingResponse> next = () => throw new InvalidOperationException("Test error");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(new PingRequest("Test"), next).AsTask());

        Assert.Equal(2, logs.Count);
        Assert.Contains("Handling PingRequest", logs[0]);
        Assert.Contains("Error handling PingRequest", logs[1]);
        Assert.Contains("Test error", logs[1]);
    }
}

public class ValidationBehaviorTests
{
    private class SuccessValidator : IValidator<PingRequest>
    {
        public ValueTask<ValidationResult> ValidateAsync(PingRequest request, CancellationToken cancellationToken = default)
        {
            return new ValueTask<ValidationResult>(ValidationResult.Success);
        }
    }

    private class FailingValidator : IValidator<PingRequest>
    {
        public ValueTask<ValidationResult> ValidateAsync(PingRequest request, CancellationToken cancellationToken = default)
        {
            return new ValueTask<ValidationResult>(
                ValidationResult.Failure(new ValidationError("Message", "Message cannot be empty")));
        }
    }

    [Fact]
    public async Task ValidationBehavior_WithValidRequest_ProceedsToHandler()
    {
        // Arrange
        var validators = new IValidator<PingRequest>[] { new SuccessValidator() };
        var behavior = new ValidationBehavior<PingRequest, PingResponse>(validators);
        var handler = new PingHandler();

        RequestHandlerDelegate<PingResponse> next = () => handler.Handle(new PingRequest("Test"));

        // Act
        var response = await behavior.Handle(new PingRequest("Test"), next);

        // Assert
        Assert.Equal("Pong: Test", response.Reply);
    }

    [Fact]
    public async Task ValidationBehavior_WithInvalidRequest_ThrowsValidationException()
    {
        // Arrange
        var validators = new IValidator<PingRequest>[] { new FailingValidator() };
        var behavior = new ValidationBehavior<PingRequest, PingResponse>(validators);
        var handler = new PingHandler();

        RequestHandlerDelegate<PingResponse> next = () => handler.Handle(new PingRequest("Test"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => behavior.Handle(new PingRequest("Test"), next).AsTask());

        Assert.Single(exception.Errors);
        Assert.Equal("Message", exception.Errors[0].PropertyName);
        Assert.Equal("Message cannot be empty", exception.Errors[0].ErrorMessage);
    }
}

public class ExceptionHandlingBehaviorTests
{
    [Fact]
    public async Task ExceptionHandlingBehavior_WithFallbackHandler_ReturnsFallbackResponse()
    {
        // Arrange
        var behavior = new ExceptionHandlingBehavior<PingRequest, PingResponse>(
            (request, ex, ct) => new ValueTask<PingResponse>(new PingResponse("Fallback")));

        RequestHandlerDelegate<PingResponse> next = () => throw new InvalidOperationException("Test error");

        // Act
        var response = await behavior.Handle(new PingRequest("Test"), next);

        // Assert
        Assert.Equal("Fallback", response.Reply);
    }

    [Fact]
    public async Task ExceptionHandlingBehavior_WithLoggingCallback_LogsAndRethrows()
    {
        // Arrange
        Exception? loggedException = null;
        var behavior = new ExceptionHandlingBehavior<PingRequest, PingResponse>(
            (request, ex) => loggedException = ex);

        RequestHandlerDelegate<PingResponse> next = () => throw new InvalidOperationException("Test error");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(new PingRequest("Test"), next).AsTask());

        Assert.NotNull(loggedException);
        Assert.Equal("Test error", loggedException.Message);
    }
}

public class RetryBehaviorTests
{
    [Fact]
    public async Task RetryBehavior_SuccessOnFirstAttempt_ReturnsImmediately()
    {
        // Arrange
        var callCount = 0;
        var behavior = new RetryBehavior<PingRequest, PingResponse>(maxRetries: 3);

        RequestHandlerDelegate<PingResponse> next = () =>
        {
            callCount++;
            return new ValueTask<PingResponse>(new PingResponse("Success"));
        };

        // Act
        var response = await behavior.Handle(new PingRequest("Test"), next);

        // Assert
        Assert.Equal("Success", response.Reply);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task RetryBehavior_SuccessAfterRetry_ReturnsAfterRetries()
    {
        // Arrange
        var callCount = 0;
        var behavior = new RetryBehavior<PingRequest, PingResponse>(
            maxRetries: 3,
            delay: TimeSpan.FromMilliseconds(1),
            shouldRetry: _ => true);

        RequestHandlerDelegate<PingResponse> next = () =>
        {
            callCount++;
            if (callCount < 3)
            {
                throw new InvalidOperationException("Retry me");
            }
            return new ValueTask<PingResponse>(new PingResponse("Success"));
        };

        // Act
        var response = await behavior.Handle(new PingRequest("Test"), next);

        // Assert
        Assert.Equal("Success", response.Reply);
        Assert.Equal(3, callCount);
    }

    [Fact]
    public async Task RetryBehavior_AllRetriesFail_ThrowsAfterMaxRetries()
    {
        // Arrange
        var callCount = 0;
        var behavior = new RetryBehavior<PingRequest, PingResponse>(
            maxRetries: 3,
            delay: TimeSpan.FromMilliseconds(1),
            shouldRetry: _ => true);

        RequestHandlerDelegate<PingResponse> next = () =>
        {
            callCount++;
            throw new InvalidOperationException("Always fails");
        };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => behavior.Handle(new PingRequest("Test"), next).AsTask());

        Assert.Equal(4, callCount); // 1 initial + 3 retries
    }
}
