# QA Agent - native-mediator

**Modelo:** Claude Sonnet 4  
**Ferramentas:** read, bash, grep  
**Foco:** Testes de handlers, notifications, behaviors, streaming

## Responsabilidades

1. **Testar Request Handler** (envio de requests, respostas corretas)
2. **Validar Notification Fan-out** (múltiplos handlers executados)
3. **Verificar Pipeline Behavior** (ordem de execução, composição)
4. **Testar Streaming** (IAsyncEnumerable, batches)
5. **Garantir ValueTask performance** (sem alocação)

## Plano de Testes

### 1. Testes de Request Handler

```csharp
public class RequestHandlerTests
{
    private readonly IMediator _mediator;
    private readonly Mock<IProductRepository> _mockRepository;
    private readonly Mock<ILogger<CreateProductCommandHandler>> _mockLogger;

    [Fact]
    public async Task Send_ValidCommand_InvokesHandler()
    {
        var command = new CreateProductCommand("Product A", 99.99m, "SKU123");
        _mockRepository
            .Setup(r => r.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        
        var response = await _mediator.Send(command);
        
        Assert.NotNull(response);
        Assert.NotEmpty(response.ProductId);
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Send_InvalidCommand_ThrowsException()
    {
        var command = new CreateProductCommand("", -10m, ""); // Name vazio, price negativo
        
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _mediator.Send(command).AsTask());
        
        Assert.Contains("required", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Send_Query_ReturnsData()
    {
        var query = new GetProductQuery("product-123");
        var expectedProduct = new ProductDto 
        { 
            Id = "product-123", 
            Name = "Product A" 
        };
        
        _mockRepository
            .Setup(r => r.GetAsync("product-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Product { Id = "product-123", Name = "Product A" });
        
        var response = await _mediator.Send(query);
        
        Assert.Equal(expectedProduct.Id, response.Id);
        Assert.Equal(expectedProduct.Name, response.Name);
    }

    [Fact]
    public async Task Send_NotFound_ReturnsNull()
    {
        var query = new GetProductQuery("non-existent");
        
        _mockRepository
            .Setup(r => r.GetAsync("non-existent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product)null);
        
        var response = await _mediator.Send(query);
        
        Assert.Null(response);
    }

    [Fact]
    public async Task Send_CancellationTokenPropagated()
    {
        var cts = new CancellationTokenSource();
        var command = new CreateProductCommand("Product", 99m, "SKU");
        
        cts.Cancel();
        
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _mediator.Send(command, cts.Token).AsTask());
    }
}
```

### 2. Testes de Notification Fan-out

```csharp
public class NotificationFanOutTests
{
    private readonly IMediator _mediator;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<IAuditLog> _mockAuditLog;
    private readonly Mock<IAnalyticsService> _mockAnalytics;

    [Fact]
    public async Task Publish_Notification_AllHandlersExecuted()
    {
        var notification = new OrderConfirmedNotification(
            "ORD123",
            "customer@example.com",
            199.99m);
        
        _mockEmailService
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        
        _mockAuditLog
            .Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        
        _mockAnalytics
            .Setup(a => a.IncrementAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        
        await _mediator.Publish(notification);
        
        _mockEmailService.Verify(
            s => s.SendAsync("customer@example.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        
        _mockAuditLog.Verify(
            a => a.LogAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
        
        _mockAnalytics.Verify(
            a => a.IncrementAsync("orders_confirmed", 1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Publish_HandlerThrows_OtherHandlersStillExecute()
    {
        var notification = new OrderConfirmedNotification("ORD123", "bad@", 99m);
        
        _mockEmailService
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Invalid email"));
        
        _mockAuditLog
            .Setup(a => a.LogAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        
        // Handler que falha não deve impedir outros
        await _mediator.Publish(notification);
        
        _mockAuditLog.Verify(
            a => a.LogAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Publish_MultipleNotifications_CorrectlyRouted()
    {
        var emailNotification = new SendEmailNotification("test@example.com");
        var smsNotification = new SendSmsNotification("+5511999999999");
        
        var emailHandlerMock = new Mock<INotificationHandler<SendEmailNotification>>();
        var smsHandlerMock = new Mock<INotificationHandler<SendSmsNotification>>();
        
        // Email handler não é invocado para SMS
        await _mediator.Publish(smsNotification);
        
        emailHandlerMock.Verify(h => h.Handle(It.IsAny<SendEmailNotification>(), It.IsAny<CancellationToken>()), Times.Never);
        smsHandlerMock.Verify(h => h.Handle(smsNotification, It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

### 3. Testes de Pipeline Behavior

```csharp
public class PipelineBehaviorTests
{
    private readonly IMediator _mediator;
    private readonly Mock<ILogger<LoggingBehavior<CreateProductCommand, CreateProductResponse>>> _mockLogger;

    [Fact]
    public async Task Behavior_LoggingBehavior_LogsRequestAndResponse()
    {
        var command = new CreateProductCommand("Product", 99m, "SKU");
        
        // Mediator com LoggingBehavior registrado
        var response = await _mediator.Send(command);
        
        // Verificar que logs foram criados
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Starting")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Behavior_ValidationBehavior_ThrowsOnInvalidRequest()
    {
        var command = new CreateProductCommand("", -50m, "");
        
        // ValidationBehavior deve interceptar e lançar exceção
        await Assert.ThrowsAsync<ValidationException>(
            () => _mediator.Send(command).AsTask());
    }

    [Fact]
    public async Task Behavior_BehaviorOrder_ExecutedInRegistrationOrder()
    {
        var executionLog = new List<string>();
        
        // Simulação de dois behaviors em ordem
        var behavior1 = new Mock<IPipelineBehavior<CreateProductCommand, CreateProductResponse>>();
        var behavior2 = new Mock<IPipelineBehavior<CreateProductCommand, CreateProductResponse>>();
        
        behavior1
            .Setup(b => b.Handle(It.IsAny<CreateProductCommand>(), It.IsAny<RequestHandlerDelegate<CreateProductResponse>>(), It.IsAny<CancellationToken>()))
            .Callback(() => executionLog.Add("Behavior1"))
            .ReturnsAsync(new CreateProductResponse("id"));
        
        behavior2
            .Setup(b => b.Handle(It.IsAny<CreateProductCommand>(), It.IsAny<RequestHandlerDelegate<CreateProductResponse>>(), It.IsAny<CancellationToken>()))
            .Callback(() => executionLog.Add("Behavior2"))
            .ReturnsAsync(new CreateProductResponse("id"));
        
        // Executar com behaviors em ordem
        var command = new CreateProductCommand("Product", 99m, "SKU");
        await _mediator.Send(command);
        
        // Verificar ordem
        Assert.Equal(new[] { "Behavior1", "Behavior2" }, executionLog);
    }

    [Fact]
    public async Task Behavior_ExceptionHandling_LogsErrorAndRethrows()
    {
        var command = new CreateProductCommand("Product", 99m, "SKU");
        
        // Setup handler para lançar exceção
        var mockHandler = new Mock<IRequestHandler<CreateProductCommand, CreateProductResponse>>();
        mockHandler
            .Setup(h => h.Handle(command, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DatabaseException("Connection failed"));
        
        // Behavior deve logar e relançar
        var ex = await Assert.ThrowsAsync<DatabaseException>(
            () => _mediator.Send(command).AsTask());
        
        Assert.Equal("Connection failed", ex.Message);
        
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}
```

### 4. Testes de Streaming

```csharp
public class StreamingTests
{
    private readonly IMediator _mediator;
    private readonly Mock<IDataRepository> _mockRepository;

    [Fact]
    public async Task CreateStream_ReturnsAsyncEnumerable()
    {
        var query = new FetchLargeDatasetQuery(10);
        var expectedData = Enumerable.Range(1, 25)
            .Select(i => new DataRecord { Id = i, Value = $"Data {i}" })
            .ToList();
        
        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(expectedData.ToAsyncEnumerable());
        
        var results = new List<DataRecord>();
        await foreach (var record in _mediator.CreateStream(query))
        {
            results.Add(record);
        }
        
        Assert.Equal(expectedData.Count, results.Count);
    }

    [Fact]
    public async Task CreateStream_BatchedCorrectly()
    {
        var query = new FetchLargeDatasetQuery(5);
        var data = Enumerable.Range(1, 12)
            .Select(i => new DataRecord { Id = i })
            .ToList();
        
        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(data.ToAsyncEnumerable());
        
        var batchCount = 0;
        await foreach (var record in _mediator.CreateStream(query))
        {
            batchCount++;
        }
        
        // 12 items com batch size 5 = 3 batches
        Assert.Equal(12, batchCount);
    }

    [Fact]
    public async Task CreateStream_CancellationToken_StopsIteration()
    {
        var query = new FetchLargeDatasetQuery(10);
        var cts = new CancellationTokenSource();
        
        var infiniteData = new AsyncEnumerable<DataRecord>(async () =>
        {
            for (int i = 0; i < 1000; i++)
            {
                await Task.Delay(10);
                yield return new DataRecord { Id = i };
            }
        });
        
        _mockRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns(infiniteData);
        
        var results = new List<DataRecord>();
        
        var enumerator = _mediator.CreateStream(query, cts.Token).GetAsyncEnumerator();
        while (await enumerator.MoveNextAsync())
        {
            results.Add(enumerator.Current);
            
            if (results.Count >= 5)
                cts.Cancel();
        }
        
        Assert.True(results.Count <= 10);
    }
}
```

### 5. Testes de ValueTask Performance

```csharp
public class ValueTaskPerformanceTests
{
    [Fact]
    public async Task Handler_ValueTask_NoAllocationWhenSynchronous()
    {
        var handler = new FastSynchronousHandler();
        var command = new FastCommand();
        
        // Medir alocações
        var before = GC.GetTotalMemory(true);
        
        for (int i = 0; i < 1000; i++)
        {
            await handler.Handle(command, CancellationToken.None);
        }
        
        var after = GC.GetTotalMemory(true);
        var allocated = after - before;
        
        // ValueTask com resultado sincronamente não deve alocar
        Assert.True(allocated < 10000); // < 10KB para 1000 execuções
    }

    [Fact]
    public async Task Handler_Behavior_ComposesEfficiently()
    {
        var mediator = CreateMediatorWithBehaviors();
        var command = new QuickCommand();
        
        var stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 10000; i++)
        {
            await mediator.Send(command);
        }
        stopwatch.Stop();
        
        // Esperar < 100ms total (10μs por execução com behaviors)
        Assert.True(stopwatch.ElapsedMilliseconds < 100);
    }
}
```

## Executar Testes

```bash
# Todos os testes
dotnet test --configuration Release

# Apenas categoria específica
dotnet test --filter "Category=Notification"

# Com cobertura
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover

# Apenas um teste
dotnet test --filter "NotificationFanOutTests"
```

## Métricas de Sucesso

- **100% testes passando**
- **Cobertura > 85%** (handlers críticos = 100%)
- **Handlers < 50ms** (com mocks)
- **Zero GC allocations** para ValueTask sincronos
- **Behaviors executeados em ordem** (verificável)

## Casos Edge Esperados

| Cenário | Comportamento Esperado |
|---------|------------------------|
| Request null | Throw ArgumentNullException |
| Notification com 0 handlers | Completa sem erro |
| Handler lança exceção | Propaga, behaviors logam |
| CancellationToken.Cancel | Throw OperationCanceledException |
| Streaming com 0 itens | IAsyncEnumerable vazio |
| Behavior falha | Exception propaga, não chama handler |
