<p align="center">
  <img src="native-mediator-icon.png" alt="NativeMediator Logo" width="200"/>
</p>

<h1 align="center">NativeMediator</h1>

<p align="center">
  A high-performance mediator pattern library for .NET, optimized for <strong>Native AOT</strong> and <strong>serverless functions</strong> (AWS Lambda, Azure Functions).
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/NativeMediator/"><img src="https://img.shields.io/nuget/v/NativeMediator.svg" alt="NuGet"/></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="License"/></a>
</p>

## ✨ Features

- 🚀 **Native AOT Compatible** - Designed from the ground up for ahead-of-time compilation
- ⚡ **High Performance** - Uses `ValueTask` for reduced allocations
- 🔌 **Dependency Injection** - First-class integration with `Microsoft.Extensions.DependencyInjection`
- 📦 **Zero Dependencies** - Only depends on DI abstractions
- 🔄 **Pipeline Behaviors** - Cross-cutting concerns like logging, validation, and retry
- 📡 **Streaming Support** - Built-in support for `IAsyncEnumerable` streaming requests
- 🎯 **CQRS Ready** - Perfect for Command Query Responsibility Segregation patterns

## 📦 Installation

```bash
dotnet add package NativeMediator
```

## 🚀 Quick Start

### 1. Define a Request and Handler

```csharp
using NativeMediator;

// Request with response
public record GetUserQuery(int UserId) : IRequest<UserDto>;

// Handler
public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserDto>
{
    public ValueTask<UserDto> Handle(GetUserQuery request, CancellationToken cancellationToken = default)
    {
        // Your logic here
        return new ValueTask<UserDto>(new UserDto(request.UserId, "John Doe"));
    }
}

public record UserDto(int Id, string Name);
```

### 2. Register Services (AOT-Compatible)

For Native AOT compatibility, register handlers explicitly:

```csharp
using NativeMediator;

services.AddNativeMediator(options =>
{
    // Explicit registration (AOT-friendly)
    options.AddHandler<GetUserQuery, UserDto, GetUserQueryHandler>();
});
```

### 3. Send Requests

```csharp
public class MyService
{
    private readonly IMediator _mediator;

    public MyService(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<UserDto> GetUser(int userId)
    {
        return await _mediator.Send(new GetUserQuery(userId));
    }
}
```

## 📚 Usage Patterns

### Requests Without Response (Commands)

```csharp
public record CreateUserCommand(string Name, string Email) : IRequest;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand>
{
    public ValueTask<Unit> Handle(CreateUserCommand request, CancellationToken cancellationToken = default)
    {
        // Create user logic
        return Unit.ValueTask;
    }
}

// Registration
options.AddHandler<CreateUserCommand, CreateUserCommandHandler>();

// Usage
await mediator.Send(new CreateUserCommand("John", "john@example.com"));
```

### Notifications (Pub/Sub)

Notifications are delivered to multiple handlers:

```csharp
public record UserCreatedNotification(int UserId, string Email) : INotification;

public class SendWelcomeEmailHandler : INotificationHandler<UserCreatedNotification>
{
    public ValueTask Handle(UserCreatedNotification notification, CancellationToken cancellationToken = default)
    {
        // Send welcome email
        return ValueTask.CompletedTask;
    }
}

public class UpdateAnalyticsHandler : INotificationHandler<UserCreatedNotification>
{
    public ValueTask Handle(UserCreatedNotification notification, CancellationToken cancellationToken = default)
    {
        // Update analytics
        return ValueTask.CompletedTask;
    }
}

// Registration
options.AddNotificationHandler<UserCreatedNotification, SendWelcomeEmailHandler>();
options.AddNotificationHandler<UserCreatedNotification, UpdateAnalyticsHandler>();

// Usage
await mediator.Publish(new UserCreatedNotification(1, "john@example.com"));
```

### Streaming Requests

For returning multiple items asynchronously:

```csharp
public record GetLogsQuery(DateTime From, DateTime To) : IStreamRequest<LogEntry>;

public class GetLogsQueryHandler : IStreamRequestHandler<GetLogsQuery, LogEntry>
{
    public async IAsyncEnumerable<LogEntry> Handle(
        GetLogsQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Stream logs from database
        await foreach (var log in _database.StreamLogsAsync(request.From, request.To, cancellationToken))
        {
            yield return log;
        }
    }
}

// Registration
options.AddStreamHandler<GetLogsQuery, LogEntry, GetLogsQueryHandler>();

// Usage
await foreach (var log in mediator.CreateStream(new GetLogsQuery(DateTime.Today, DateTime.Now)))
{
    Console.WriteLine(log);
}
```

## 🔧 Pipeline Behaviors

Pipeline behaviors allow you to add cross-cutting concerns around request handling:

### Built-in Behaviors

#### Logging Behavior

```csharp
using NativeMediator.Behaviors;

options.AddBehavior<MyRequest, MyResponse, LoggingBehavior<MyRequest, MyResponse>>();
```

#### Validation Behavior

```csharp
using NativeMediator.Behaviors;

// Implement IValidator<TRequest>
public class CreateUserValidator : IValidator<CreateUserCommand>
{
    public ValueTask<ValidationResult> ValidateAsync(
        CreateUserCommand request, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(request.Name))
        {
            return new ValueTask<ValidationResult>(
                ValidationResult.Failure(new ValidationError("Name", "Name is required")));
        }
        return new ValueTask<ValidationResult>(ValidationResult.Success);
    }
}

// Register validator and behavior
services.AddScoped<IValidator<CreateUserCommand>, CreateUserValidator>();
options.AddBehavior<CreateUserCommand, Unit, ValidationBehavior<CreateUserCommand, Unit>>();
```

#### Retry Behavior

```csharp
using NativeMediator.Behaviors;

// Custom retry behavior
services.AddScoped<IPipelineBehavior<MyRequest, MyResponse>>(sp =>
    new RetryBehavior<MyRequest, MyResponse>(
        maxRetries: 3,
        delay: TimeSpan.FromSeconds(1),
        shouldRetry: ex => ex is HttpRequestException));
```

#### Exception Handling Behavior

```csharp
using NativeMediator.Behaviors;

services.AddScoped<IPipelineBehavior<MyRequest, MyResponse>>(sp =>
    new ExceptionHandlingBehavior<MyRequest, MyResponse>(
        (request, ex, ct) => new ValueTask<MyResponse>(new MyResponse { Error = ex.Message })));
```

### Custom Pipeline Behavior

```csharp
public class TimingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<TimingBehavior<TRequest, TResponse>> _logger;

    public TimingBehavior(ILogger<TimingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            return await next();
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "Request {RequestName} took {ElapsedMs}ms",
                typeof(TRequest).Name,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
```

## ☁️ Serverless Examples

### AWS Lambda

```csharp
public class Function
{
    private readonly IMediator _mediator;

    public Function()
    {
        var services = new ServiceCollection();
        services.AddNativeMediator(options =>
        {
            options.MediatorLifetime = ServiceLifetime.Singleton;
            options.AddHandler<ProcessOrderCommand, OrderResult, ProcessOrderHandler>();
        });
        
        var provider = services.BuildServiceProvider();
        _mediator = provider.GetRequiredService<IMediator>();
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(
        APIGatewayProxyRequest request, 
        ILambdaContext context)
    {
        var command = JsonSerializer.Deserialize<ProcessOrderCommand>(request.Body);
        var result = await _mediator.Send(command);
        
        return new APIGatewayProxyResponse
        {
            StatusCode = 200,
            Body = JsonSerializer.Serialize(result)
        };
    }
}
```

### Azure Functions (Isolated Worker)

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddNativeMediator(options =>
        {
            options.AddHandler<ProcessOrderCommand, OrderResult, ProcessOrderHandler>();
        });
    })
    .Build();

host.Run();

public class OrderFunction
{
    private readonly IMediator _mediator;

    public OrderFunction(IMediator mediator)
    {
        _mediator = mediator;
    }

    [Function("ProcessOrder")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
    {
        var command = await req.ReadFromJsonAsync<ProcessOrderCommand>();
        var result = await _mediator.Send(command);
        
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }
}
```

## ⚙️ Configuration Options

```csharp
services.AddNativeMediator(options =>
{
    // Mediator lifetime (default: Scoped)
    options.MediatorLifetime = ServiceLifetime.Scoped;
    
    // Default handler lifetime (default: Scoped)
    options.DefaultHandlerLifetime = ServiceLifetime.Scoped;
    
    // Explicit handler registration (AOT-compatible)
    options.AddHandler<MyRequest, MyResponse, MyHandler>();
    options.AddHandler<MyCommand, MyCommandHandler>(); // For IRequest (void)
    options.AddNotificationHandler<MyNotification, MyNotificationHandler>();
    options.AddStreamHandler<MyStreamRequest, MyItem, MyStreamHandler>();
    
    // Pipeline behaviors
    options.AddBehavior<MyRequest, MyResponse, MyBehavior>();
    
    // Assembly scanning (NOT AOT-compatible, use for JIT scenarios only)
    options.AddHandlersFromAssemblyContaining<Program>();
});
```

## 🎯 Native AOT Best Practices

1. **Use explicit registration** instead of assembly scanning:
   ```csharp
   // ✅ AOT-compatible
   options.AddHandler<MyRequest, MyResponse, MyHandler>();
   
   // ❌ Not AOT-compatible (uses reflection)
   options.AddHandlersFromAssemblyContaining<Program>();
   ```

2. **Use concrete types** in your handlers and avoid dynamic type creation.

3. **Avoid generic open types** in pipeline behaviors when targeting AOT.

4. **Test with PublishAot** enabled during development:
   ```xml
   <PropertyGroup>
     <PublishAot>true</PublishAot>
   </PropertyGroup>
   ```

## 🔄 Comparison with Other Libraries

| Feature | NativeMediator | MediatR | WolverineFx |
|---------|---------------|---------|-------------|
| Native AOT | ✅ First-class | ⚠️ Limited | ⚠️ Limited |
| ValueTask | ✅ Yes | ❌ Task | ✅ Yes |
| Serverless Optimized | ✅ Yes | ❌ No | ❌ No |
| Pipeline Behaviors | ✅ Yes | ✅ Yes | ✅ Yes |
| Streaming | ✅ Yes | ✅ Yes | ✅ Yes |
| Notifications | ✅ Yes | ✅ Yes | ✅ Yes |
| Dependencies | Minimal | Minimal | Many |

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📮 Support

- 📧 Create an issue on [GitHub](https://github.com/swepay/native-mediator/issues)
- ⭐ Star this repository if you find it useful!
