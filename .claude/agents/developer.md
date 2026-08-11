---
name: developer
archetype: support-library
model: claude-sonnet-5
tools: [Read, Write, Edit, Bash, Grep, Glob]
description: >
  Implemente features seguindo o shared kernel e as convenções de código.
---

# Developer Agent - native-mediator

**Modelo:** Claude Sonnet 4  
**Ferramentas:** read, write, bash, edit, grep, glob  
**Foco:** Implementar handlers, behaviors, notifications, streaming

## Responsabilidades

1. **Adicionar Request Handler** (Command/Query com resposta única)
2. **Criar Notification Handler** (fan-out para múltiplos subscribers)
3. **Implementar Pipeline Behavior** (logging, validação, timing)
4. **Configurar Streaming Query** (resultados paginados)
5. **Registrar explicitamente** (ZERO scanning, tudo manual)

## Fluxo de Trabalho

### Adicionar Handler para Request

1. **Abrir CLAUDE.md** para revisar padrão de RequestHandler
2. **Criar Request record** em `/src/Requests/Commands` ou `/src/Requests/Queries`
3. **Criar Handler class** em `/src/Handlers`
4. **Usar ValueTask** (nunca Task puro)
5. **Registrar em ServiceCollection** (chave!)
6. **Testar com xUnit**

### Estrutura do Request Handler

```csharp
// 1. Request
public record CreateProductCommand(
    string Name,
    decimal Price,
    string Sku) 
    : IRequest<CreateProductResponse>;

public record CreateProductResponse(string ProductId);

// 2. Handler
public class CreateProductCommandHandler 
    : IRequestHandler<CreateProductCommand, CreateProductResponse>
{
    private readonly IProductRepository _repository;
    private readonly ILogger<CreateProductCommandHandler> _logger;

    public CreateProductCommandHandler(
        IProductRepository repository,
        ILogger<CreateProductCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async ValueTask<CreateProductResponse> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating product: {Name} ({Sku})", 
            request.Name, 
            request.Sku);
        
        // Validações rápidas
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Product name is required");
        
        if (request.Price <= 0)
            throw new ArgumentException("Price must be positive");
        
        // Processar
        var productId = Guid.NewGuid().ToString();
        var product = new Product
        {
            Id = productId,
            Name = request.Name,
            Price = request.Price,
            Sku = request.Sku,
            CreatedAt = DateTime.UtcNow
        };
        
        await _repository.SaveAsync(product, cancellationToken);
        
        _logger.LogInformation("Product created: {ProductId}", productId);
        
        return new CreateProductResponse(productId);
    }
}

// 3. Registrar
services.AddSingleton<IRequestHandler<CreateProductCommand, CreateProductResponse>,
    CreateProductCommandHandler>();
```

### Criar Notification Handler

```csharp
// 1. Notification
public record OrderConfirmedNotification(
    string OrderId,
    string CustomerEmail,
    decimal Total)
    : INotification;

// 2. Handler 1: Enviar email
public class SendOrderConfirmationEmailHandler 
    : INotificationHandler<OrderConfirmedNotification>
{
    private readonly IEmailService _emailService;

    public async ValueTask Handle(
        OrderConfirmedNotification notification,
        CancellationToken cancellationToken)
    {
        await _emailService.SendAsync(
            notification.CustomerEmail,
            $"Order {notification.OrderId} confirmed",
            $"Total: ${notification.Total}",
            cancellationToken);
    }
}

// 3. Handler 2: Log no audit
public class AuditOrderConfirmationHandler 
    : INotificationHandler<OrderConfirmedNotification>
{
    private readonly IAuditLog _auditLog;

    public async ValueTask Handle(
        OrderConfirmedNotification notification,
        CancellationToken cancellationToken)
    {
        await _auditLog.LogAsync(
            $"Order confirmed: {notification.OrderId}",
            new { notification.CustomerId, notification.Total },
            cancellationToken);
    }
}

// 4. Handler 3: Atualizar analytics
public class UpdateOrderAnalyticsHandler 
    : INotificationHandler<OrderConfirmedNotification>
{
    private readonly IAnalyticsService _analytics;

    public async ValueTask Handle(
        OrderConfirmedNotification notification,
        CancellationToken cancellationToken)
    {
        await _analytics.IncrementAsync(
            "orders_confirmed",
            1,
            cancellationToken);
    }
}

// 5. Registrar todos
services.AddSingleton<INotificationHandler<OrderConfirmedNotification>,
    SendOrderConfirmationEmailHandler>();
services.AddSingleton<INotificationHandler<OrderConfirmedNotification>,
    AuditOrderConfirmationHandler>();
services.AddSingleton<INotificationHandler<OrderConfirmedNotification>,
    UpdateOrderAnalyticsHandler>();
```

### Criar Pipeline Behavior

```csharp
// Logging behavior
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
        var requestName = typeof(TRequest).Name;
        _logger.LogInformation("Starting {Request}", requestName);
        
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await next();
            stopwatch.Stop();
            
            _logger.LogInformation(
                "Completed {Request} in {Elapsed}ms",
                requestName,
                stopwatch.ElapsedMilliseconds);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed {Request}", requestName);
            throw;
        }
    }
}

// Validação behavior
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
        {
            var errors = string.Join(", ", 
                result.Errors.Select(e => e.ErrorMessage));
            throw new ValidationException($"Validation failed: {errors}");
        }
        
        return await next();
    }
}

// Registrar behaviors (ordem importa!)
services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

### Criar Streaming Handler

```csharp
// 1. Stream Request
public record FetchLargeDatasetQuery(int BatchSize)
    : IStreamRequest<DataRecord>;

// 2. Stream Handler
public class FetchLargeDatasetHandler 
    : IStreamRequestHandler<FetchLargeDatasetQuery, DataRecord>
{
    private readonly IDataRepository _repository;

    public async IAsyncEnumerable<DataRecord> Handle(
        FetchLargeDatasetQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var items = _repository.GetAllAsync(cancellationToken);
        var batch = new List<DataRecord>();
        
        await foreach (var item in items)
        {
            batch.Add(item);
            
            if (batch.Count >= request.BatchSize)
            {
                foreach (var record in batch)
                {
                    yield return record;
                }
                batch.Clear();
            }
        }
        
        // Retornar últimos itens
        foreach (var record in batch)
        {
            yield return record;
        }
    }
}

// 3. Registrar
services.AddSingleton<IStreamRequestHandler<FetchLargeDatasetQuery, DataRecord>,
    FetchLargeDatasetHandler>();

// 4. Usar em Lambda
var mediator = sp.GetRequiredService<IMediator>();
await foreach (var record in mediator.CreateStream(
    new FetchLargeDatasetQuery(100)))
{
    // Processar registro
    await ProcessRecordAsync(record);
}
```

## Checklist Antes de Submeter

- [ ] Request class criado (Command ou Query)
- [ ] Handler class implementado com `ValueTask`
- [ ] Registrado em ServiceCollection (sem scanning!)
- [ ] Tests xUnit com behaviors mockados (NSubstitute)
- [ ] `dotnet test` passando 100%
- [ ] `dotnet build` sem warnings
- [ ] Sem reflection ou scanning automático
- [ ] Cancellation token propagado corretamente
- [ ] Logging apropriado no handler
- [ ] Erros documentados (throw exceptions)

## Padrões Importantes

### Fire-and-Forget Handler
```csharp
public record SendNotificationCommand(string UserId, string Message)
    : IRequest; // Sem resposta!

public class SendNotificationHandler 
    : IRequestHandler<SendNotificationCommand>
{
    public async ValueTask Handle(SendNotificationCommand request, CancellationToken ct)
    {
        await _notificationService.SendAsync(request.UserId, request.Message, ct);
    }
}
```

### Behavior com Contexto
```csharp
public class AuthorizationBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>, IAuthorizedRequest
{
    public async ValueTask<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_authService.IsAuthorizedFor(request.RequiredRole))
            throw new UnauthorizedException();
        
        return await next();
    }
}
```

## Testes Esperados

```csharp
[Fact]
public async Task CreateProductCommandHandler_ValidInput_ReturnsProductId()
{
    var handler = new CreateProductCommandHandler(_mockRepository.Object, _mockLogger.Object);
    var command = new CreateProductCommand("Product A", 99.99m, "SKU123");
    
    var response = await handler.Handle(command, CancellationToken.None);
    
    Assert.NotNull(response);
    Assert.NotEmpty(response.ProductId);
    _mockRepository.Verify(r => r.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task OrderConfirmedNotification_MultipleHandlers_AllExecuted()
{
    var handler1 = new Mock<INotificationHandler<OrderConfirmedNotification>>();
    var handler2 = new Mock<INotificationHandler<OrderConfirmedNotification>>();
    
    var notification = new OrderConfirmedNotification("ORD123", "test@example.com", 100m);
    
    await handler1.Object.Handle(notification, CancellationToken.None);
    await handler2.Object.Handle(notification, CancellationToken.None);
    
    handler1.Verify(h => h.Handle(notification, It.IsAny<CancellationToken>()), Times.Once);
    handler2.Verify(h => h.Handle(notification, It.IsAny<CancellationToken>()), Times.Once);
}
```

## Links Úteis

- **CLAUDE.md:** Referência de API
- **ValueTask docs:** Microsoft Docs
- **CQRS pattern:** Documentação interna
