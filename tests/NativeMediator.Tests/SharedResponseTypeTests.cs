using Microsoft.Extensions.DependencyInjection;

namespace NativeMediator.Tests;

/// <summary>
/// Tests for multiple request types sharing the same response type.
/// This reproduces the bug where LoginWithPasswordCommand and RefreshTokenCommand
/// both return LoginResponse, but the wrong handler gets invoked.
/// </summary>
public class SharedResponseTypeTests
{
    // Simulating the real-world scenario with LoginResponse
    public record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn);

    // Two different commands that share the same response type
    public record LoginWithPasswordCommand(
        string RealmId,
        string ClientId,
        string UsernameOrEmail,
        string Password
    ) : IRequest<LoginResponse>;

    public record RefreshTokenCommand(
        string RealmId,
        string ClientId,
        string RefreshToken
    ) : IRequest<LoginResponse>;

    // Handler for LoginWithPasswordCommand
    public class LoginWithPasswordHandler : IRequestHandler<LoginWithPasswordCommand, LoginResponse>
    {
        public static int CallCount { get; private set; }
        public static void Reset() => CallCount = 0;

        public ValueTask<LoginResponse> Handle(LoginWithPasswordCommand request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return new ValueTask<LoginResponse>(new LoginResponse(
                AccessToken: $"access_token_for_{request.UsernameOrEmail}",
                RefreshToken: "refresh_token_password",
                ExpiresIn: 3600
            ));
        }
    }

    // Handler for RefreshTokenCommand
    public class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, LoginResponse>
    {
        public static int CallCount { get; private set; }
        public static void Reset() => CallCount = 0;

        public ValueTask<LoginResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return new ValueTask<LoginResponse>(new LoginResponse(
                AccessToken: $"refreshed_access_token",
                RefreshToken: "new_refresh_token",
                ExpiresIn: 3600
            ));
        }
    }

    [Fact]
    public async Task Send_LoginWithPasswordCommand_ShouldInvokeLoginWithPasswordHandler()
    {
        // Arrange
        LoginWithPasswordHandler.Reset();
        RefreshTokenHandler.Reset();

        var services = new ServiceCollection();
        services.AddNativeMediator(options =>
        {
            options.AddHandler<LoginWithPasswordCommand, LoginResponse, LoginWithPasswordHandler>();
            options.AddHandler<RefreshTokenCommand, LoginResponse, RefreshTokenHandler>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var response = await mediator.Send(new LoginWithPasswordCommand(
            RealmId: "realm1",
            ClientId: "client1",
            UsernameOrEmail: "user@example.com",
            Password: "password123"
        ));

        // Assert
        Assert.Equal(1, LoginWithPasswordHandler.CallCount);
        Assert.Equal(0, RefreshTokenHandler.CallCount);
        Assert.Contains("user@example.com", response.AccessToken);
    }

    [Fact]
    public async Task Send_RefreshTokenCommand_ShouldInvokeRefreshTokenHandler()
    {
        // Arrange
        LoginWithPasswordHandler.Reset();
        RefreshTokenHandler.Reset();

        var services = new ServiceCollection();
        services.AddNativeMediator(options =>
        {
            options.AddHandler<LoginWithPasswordCommand, LoginResponse, LoginWithPasswordHandler>();
            options.AddHandler<RefreshTokenCommand, LoginResponse, RefreshTokenHandler>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var response = await mediator.Send(new RefreshTokenCommand(
            RealmId: "realm1",
            ClientId: "client1",
            RefreshToken: "old_refresh_token"
        ));

        // Assert
        Assert.Equal(0, LoginWithPasswordHandler.CallCount);
        Assert.Equal(1, RefreshTokenHandler.CallCount);
        Assert.Equal("refreshed_access_token", response.AccessToken);
    }

    [Fact]
    public async Task Send_BothCommands_ShouldInvokeCorrectHandlers()
    {
        // Arrange
        LoginWithPasswordHandler.Reset();
        RefreshTokenHandler.Reset();

        var services = new ServiceCollection();
        services.AddNativeMediator(options =>
        {
            options.AddHandler<LoginWithPasswordCommand, LoginResponse, LoginWithPasswordHandler>();
            options.AddHandler<RefreshTokenCommand, LoginResponse, RefreshTokenHandler>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act - Send LoginWithPasswordCommand first
        var loginResponse = await mediator.Send(new LoginWithPasswordCommand(
            RealmId: "realm1",
            ClientId: "client1",
            UsernameOrEmail: "user@example.com",
            Password: "password123"
        ));

        // Act - Then send RefreshTokenCommand
        var refreshResponse = await mediator.Send(new RefreshTokenCommand(
            RealmId: "realm1",
            ClientId: "client1",
            RefreshToken: "old_refresh_token"
        ));

        // Assert
        Assert.Equal(1, LoginWithPasswordHandler.CallCount);
        Assert.Equal(1, RefreshTokenHandler.CallCount);
        Assert.Contains("user@example.com", loginResponse.AccessToken);
        Assert.Equal("refreshed_access_token", refreshResponse.AccessToken);
    }

    [Fact]
    public async Task Send_CommandsInReverseOrder_ShouldStillInvokeCorrectHandlers()
    {
        // Arrange
        LoginWithPasswordHandler.Reset();
        RefreshTokenHandler.Reset();

        var services = new ServiceCollection();
        services.AddNativeMediator(options =>
        {
            // Register in reverse order
            options.AddHandler<RefreshTokenCommand, LoginResponse, RefreshTokenHandler>();
            options.AddHandler<LoginWithPasswordCommand, LoginResponse, LoginWithPasswordHandler>();
        });

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act - Send RefreshTokenCommand first
        var refreshResponse = await mediator.Send(new RefreshTokenCommand(
            RealmId: "realm1",
            ClientId: "client1",
            RefreshToken: "old_refresh_token"
        ));

        // Act - Then send LoginWithPasswordCommand
        var loginResponse = await mediator.Send(new LoginWithPasswordCommand(
            RealmId: "realm1",
            ClientId: "client1",
            UsernameOrEmail: "user@example.com",
            Password: "password123"
        ));

        // Assert
        Assert.Equal(1, LoginWithPasswordHandler.CallCount);
        Assert.Equal(1, RefreshTokenHandler.CallCount);
    }
}
