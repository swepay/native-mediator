# native-mediator

**Versão:** v1.0.4  
**Tipo:** NuGet Library - CQRS Mediator AOT-Safe  
**AOT-Safe:** Sim  
**Linguagem:** C# 12+

## O que é

`native-mediator` é um padrão CQRS (Command Query Responsibility Segregation) implementado como mediator type-safe. Diferente do MediatR, é otimizado para AWS Lambda com AOT compilation - sem reflection scanning, registro explícito obrigatório, e `ValueTask` para performance.

## Por que ValueTask?

Em Lambda:
- **Latência importa:** Executar comando em <50ms é crítico
- **Memory allocation:** ValueTask evita heap allocation para operações já completas
- **Budget:** Lambda cobra por 100ms - economizar milissegundos = $ economizado

```csharp
// ❌ Ruim: async Task aloca sempre
public async Task<Result> HandleAsync(Command cmd) => ...

// ✅ Bom: ValueTask não aloca se completa sincronamente  
public async ValueTask<Result> HandleAsync(Command cmd) => ...
```

## API Pública Principal

### IMediator
Interface central para enviar requisições.

```csharp
public interface IMediator
{
    // Enviar command/query e obter resposta
    ValueTask<TResponse> Send<TResponse>(
        IRequest<TResponse> request, 
        CancellationToken cancellationToken = default);
    
    // Publicar notificação para múltiplos handlers
    ValueTask Publish<TNotification>(
        TNotification notification, 
        CancellationToken cancellationToken = default) 
        where TNotification : INotification;
    
    // Streaming de múltiplos resultados
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request, 
        CancellationToken cancellationToken = default);
}
```

### IRequestHandler
Handler para command/query único com resposta.

```csharp
public interface IRequestHandler<TRequest, TResponse> 
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

// Fire-and-forget (sem resposta)
public interface IRequestHandler<TRequest> 
    where TRequest : IRequest
{
    ValueTask Handle(TRequest request, CancellationToken cancellationToken);
}
```

### INotificationHandler
Handler para notificação (publicada para múltiplos subscribers).

```csharp
public interface INotificationHandler<TNotification> 
    where TNotification : INotification
{
    ValueTask Handle(TNotification notification, CancellationToken cancellationToken);
}
```

### IStreamRequestHandler
Handler que retorna stream de resultados.

```csharp
public interface IStreamRequestHandler<TRequest, TResponse> 
    where TRequest : IStreamRequest<TResponse>
{
    IAsyncEnumerable<TResponse> Handle(
        TRequest request, 
        CancellationToken cancellationToken);
}
```

### IPipelineBehavior
Middleware que intercepta requisições (logging, validação, etc).

```csharp
public interface IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
```

## Como Usar

### 1. Definir Request/Command/Query

```csharp
// Command (muta estado)
public record CreateUserCommand(
    string Name, 
    string Email) 
    : IRequest<CreateUserResponse>;

// Query (apenas lê)
public record GetUserQuery(string UserId) 
    : IRequest<UserDto>;

// Notificação (publicada para múltiplos handlers)
public record UserCreatedNotification(
    string UserId, 
    string Email) 
    : INotification;

// Streaming query
public record FetchUsersPagesQuery(int PageSize) 
    : IStreamRequest<UserDto>;
```

### 2. Criar Handlers

```csharp
// Command handler com resposta
public class CreateUserCommandHandler 
    : IRequestHandler<CreateUserCommand, CreateUserResponse>
{
    private readonly IUserRepository _repository;

    public async ValueTask<CreateUserResponse> Handle(
        CreateUserCommand request, 
        CancellationToken cancellationToken)
    {
        var userId = Guid.NewGuid().ToString();
        await _repository.SaveAsync(new User { Id = userId, Email = request.Email }, cancellationToken);
        
        return new CreateUserResponse(userId);
    }
}

// Query handler
public class GetUserQueryHandler 
    : IRequestHandler<GetUserQuery, UserDto>
{
    private readonly IUserRepository _repository;

    public async ValueTask<UserDto> Handle(
        GetUserQuery request, 
        CancellationToken cancellationToken)
    {
        var user = await _repository.GetAsync(request.UserId, cancellationToken);
        return new UserDto { Id = user.Id, Name = user.Name };
    }
}

// Fire-and-forget handler
public class LogCommandHandler 
    : IRequestHandler<CreateUserCommand>
{
    private readonly ILogger<LogCommandHandler> _logger;

    public ValueTask Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating user: {Email}", request.Email);
        return ValueTask.CompletedTask;
    }
}

// Notification handler (fan-out)
public class SendWelcomeEmailHandler 
    : INotificationHandler<UserCreatedNotification>
{
    private readonly IEmailService _emailService;

    public async ValueTask Handle(
        UserCreatedNotification notification, 
        CancellationToken cancellationToken)
    {
        await _emailService.SendWelcomeAsync(notification.Email, cancellationToken);
    }
}

public class LogUserCreatedHandler 
    : INotificationHandler<UserCreatedNotification>
{
    private readonly IAuditLog _auditLog;

    public async ValueTask Handle(
        UserCreatedNotification notification, 
        CancellationToken cancellationToken)
    {
        await _auditLog.LogAsync($"User created: {notification.UserId}", cancellationToken);
    }
}

// Streaming handler
public class FetchUsersPagesHandler 
    : IStreamRequestHandler<FetchUsersPagesQuery, UserDto>
{
    private readonly IUserRepository _repository;

    public async IAsyncEnumerable<UserDto> Handle(
        FetchUsersPagesQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var users = _repository.GetAllAsync(cancellationToken);
        
        var batch = new List<UserDto>();
        await foreach (var user in users)
        {
            batch.Add(new UserDto { Id = user.Id, Name = user.Name });
            if (batch.Count >= request.PageSize)
            {
                yield return batch.Count > 0 ? batch[0] : null;
                batch.Clear();
            }
        }
    }
}
```

### 3. Criar Pipeline Behavior (Middleware)

```csharp
public class LoggingBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting request: {Request}", typeof(TRequest).Name);
        
        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();
        
        _logger.LogInformation(
            "Finished request: {Request} in {Elapsed}ms", 
            typeof(TRequest).Name, 
            stopwatch.ElapsedMilliseconds);
        
        return response;
    }
}

public class ValidationBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IValidator<TRequest> _validator;

    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var result = await _validator.ValidateAsync(request, cancellationToken);
        
        if (!result.IsValid)
            throw new ValidationException(result.Errors);
        
        return await next();
    }
}
```

### 4. Registrar EXPLÍCITO (sem scanning!)

```csharp
public static partial class ServiceCollectionExtensions
{
    [RegisterServices]
    public static IServiceCollection AddNativeMediator(
        this IServiceCollection services)
    {
        // Mediator core
        services.AddSingleton<IMediator, Mediator>();
        
        // Request Handlers
        services.AddSingleton<IRequestHandler<CreateUserCommand, CreateUserResponse>, 
            CreateUserCommandHandler>();
        services.AddSingleton<IRequestHandler<GetUserQuery, UserDto>, 
            GetUserQueryHandler>();
        
        // Notification Handlers (múltiplos)
        services.AddSingleton<INotificationHandler<UserCreatedNotification>, 
            SendWelcomeEmailHandler>();
        services.AddSingleton<INotificationHandler<UserCreatedNotification>, 
            LogUserCreatedHandler>();
        
        // Pipeline Behaviors
        services.AddSingleton(typeof(IPipelineBehavior<,>), 
            typeof(LoggingBehavior<,>));
        services.AddSingleton(typeof(IPipelineBehavior<,>), 
            typeof(ValidationBehavior<,>));
        
        return services;
    }
}
```

### 5. Usar em Lambda

```csharp
public async Task<HttpResponse> FunctionHandler(
    HttpRequest request, 
    ILambdaContext context)
{
    var mediator = _serviceProvider.GetRequiredService<IMediator>();
    
    // Enviar command
    var response = await mediator.Send(
        new CreateUserCommand("John", "john@example.com"));
    
    // Publicar notificação
    await mediator.Publish(
        new UserCreatedNotification(response.UserId, "john@example.com"));
    
    // Query
    var user = await mediator.Send(
        new GetUserQuery(response.UserId));
    
    return HttpResponse.Ok(user);
}
```

## Registro Explícito vs. Scanning

| Aspecto | Explícito (Obrigatório) | Scanning (Proibido) |
|---------|-------------------------|-------------------|
| AOT-Safe | ✅ Sim | ❌ Não (reflection) |
| Boilerplate | ❌ Mais | ✅ Menos |
| Segurança | ✅ Type-safe | ❌ Runtime magic |
| Performance | ✅ Rápido | ❌ Lento (startup) |
| Lambda custo | ✅ Menor | ❌ Maior |

## Premissas

- **ValueTask obrigatório:** Todos os handlers retornam `ValueTask` ou `ValueTask<T>`
- **Registro explícito:** Sem scanning automático, TUDO é registrado manualmente
- **Sem MediatR:** Implementação nativa, não use o pacote MediatR
- **Namespace:** `Native.Mediator`
- **Target:** `net8.0`; PublishAot = true
- **Async all the way:** Sem `.Result` ou `.Wait()`

## Terminologia

- **Request:** Command ou Query (tem handler único)
- **Notification:** Publicação fan-out (múltiplos handlers)
- **Behavior:** Middleware interceptando requisição
- **Streaming:** Múltiplos resultados via IAsyncEnumerable
- **ValueTask:** Retorno que evita alocação se sincronamente completo

## Limitações

- Um handler por Request (mas múltiplos por Notification)
- Behaviors executam na ordem de registro
- Streaming não funciona com behaviors
- Cancellation token é propagado automaticamente
