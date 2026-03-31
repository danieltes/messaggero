# Quickstart: Messaggero

**Feature**: 00001-broker-agnostic-core  
**Date**: 2026-03-29

## Install

```bash
# Core library
dotnet add package Messaggero

# Broker adapters (add the ones you need)
dotnet add package Messaggero.Kafka
dotnet add package Messaggero.RabbitMQ

# For testing without brokers
dotnet add package Messaggero.Testing
```

---

## 1. Define a Message

```csharp
public sealed class OrderPlaced
{
    public required string OrderId { get; init; }
    public required decimal Total { get; init; }
}
```

---

## 2. Implement a Handler

```csharp
using Messaggero.Abstractions;

public sealed class OrderPlacedHandler : IMessageHandler<OrderPlaced>
{
    public Task HandleAsync(
        OrderPlaced message,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Order {message.OrderId} received via {context.SourceTransport}");
        return Task.CompletedTask;
    }
}
```

---

## 3. Configure with Fluent Builder

### Single Transport (Kafka only)

```csharp
using Messaggero.Kafka;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMessaggero(messaging =>
{
    messaging
        .AddKafka("kafka", kafka =>
        {
            kafka.BootstrapServers = "localhost:9092";
            kafka.GroupId = "my-service";
        })
        .Route<OrderPlaced>(r => r.ToTransport("kafka"))
        .RegisterHandler<OrderPlacedHandler, OrderPlaced>()
        .EnableObservability();
});

var app = builder.Build();
await app.RunAsync();
```

### Multiple Transports (Kafka + RabbitMQ)

```csharp
using Messaggero.Kafka;
using Messaggero.RabbitMQ;

builder.Services.AddMessaggero(messaging =>
{
    messaging
        .AddKafka("kafka", kafka =>
        {
            kafka.BootstrapServers = "localhost:9092";
            kafka.GroupId = "my-service";
        })
        .AddRabbitMQ("rabbitmq", rabbit =>
        {
            rabbit.HostName = "localhost";
            rabbit.Port = 5672;
        })
        // Route different message types to different brokers
        .Route<OrderPlaced>(r => r.ToTransport("kafka"))
        .Route<EmailRequested>(r => r.ToTransport("rabbitmq"))
        // Fan-out: send to both brokers
        .Route<AuditEvent>(r => r
            .ToTransport("kafka")
            .ToTransport("rabbitmq"))
        .RegisterHandler<OrderPlacedHandler, OrderPlaced>()
        .RegisterHandler<EmailHandler, EmailRequested>()
        .EnableObservability();
});
```

---

## 4. Publish Messages

```csharp
public class OrderController(IMessageBus bus)
{
    public async Task<IResult> PlaceOrder(PlaceOrderRequest request)
    {
        var order = ProcessOrder(request);

        // Library resolves the target transport automatically
        var result = await bus.PublishAsync(new OrderPlaced
        {
            OrderId = order.Id,
            Total = order.Total
        });

        return result.IsSuccess
            ? Results.Ok(order.Id)
            : Results.Problem("Failed to publish order event");
    }
}
```

---

## 5. Override Destination

By default, the destination name is the message type in lowercase (e.g., `OrderPlaced` → `orderplaced`).
Use `ToDestination` to override the topic/queue name or provide transport-specific overrides:

### Custom Topic/Queue Name

```csharp
builder.Services.AddMessaggero(messaging =>
{
    messaging
        .AddKafka("kafka", kafka =>
        {
            kafka.BootstrapServers = "localhost:9092";
            kafka.GroupId = "my-service";
        })
        // Publish OrderPlaced to Kafka topic "orders-v2" instead of "orderplaced"
        .Route<OrderPlaced>(r => r
            .ToTransport("kafka")
            .ToDestination("orders-v2"))
        .RegisterHandler<OrderPlacedHandler, OrderPlaced>();
});
```

### Transport-Specific Overrides

Pass adapter-specific settings (e.g., RabbitMQ routing key) via `TransportOverrides`:

```csharp
builder.Services.AddMessaggero(messaging =>
{
    messaging
        .AddRabbitMQ("rabbitmq", rabbit =>
        {
            rabbit.HostName = "localhost";
        })
        // Publish to exchange "events" with routing key "orders.placed"
        .Route<OrderPlaced>(r => r
            .ToTransport("rabbitmq")
            .ToDestination("events", new Dictionary<string, string>
            {
                ["routingKey"] = "orders.placed"
            }))
        .RegisterHandler<OrderPlacedHandler, OrderPlaced>();
});
```

The `TransportOverrides` dictionary is adapter-specific:

| Adapter | Key | Effect |
|---------|-----|--------|
| RabbitMQ | `routingKey` | Sets the AMQP routing key (defaults to destination name) |
| Kafka | *(none currently)* | Kafka uses the destination name as the topic directly |

---

## 6. Testing Without Brokers

```csharp
using Messaggero.Abstractions;
using Messaggero.Testing;
using Messaggero.Model;

[Fact]
public async Task OrderPlacedHandler_processes_message()
{
    // Unit test — handler in isolation, no library host needed
    var handler = new OrderPlacedHandler();
    var message = new OrderPlaced { OrderId = "123", Total = 99.99m };
    var context = new MessageContext
    {
        MessageId = "msg-1",
        MessageType = nameof(OrderPlaced),
        SourceTransport = "test",
        Headers = new MessageHeaders(),
        Timestamp = DateTimeOffset.UtcNow,
        DeliveryAttempt = 1
    };

    await handler.HandleAsync(message, context, CancellationToken.None);
}

[Fact]
public async Task TestMessageBus_captures_published_messages()
{
    // Use TestMessageBus to capture and assert on published messages
    var testBus = new TestMessageBus();

    var result = await testBus.PublishAsync(new OrderPlaced
    {
        OrderId = "456",
        Total = 50.00m
    });

    Assert.True(result.IsSuccess);
    testBus.AssertPublished<OrderPlaced>();

    var published = testBus.GetPublishedMessages<OrderPlaced>();
    Assert.Single(published);
    Assert.Equal("456", published[0].OrderId);
}
```

---

## 7. Advanced Configuration

### Custom Retry Policy

```csharp
messaging.AddKafka("kafka", kafka =>
{
    kafka.BootstrapServers = "localhost:9092";
    kafka.RetryPolicy = new RetryPolicyOptions
    {
        MaxAttempts = 5,
        BackoffStrategy = BackoffStrategy.Exponential,
        InitialDelay = TimeSpan.FromSeconds(1),
        MaxDelay = TimeSpan.FromSeconds(30),
        DeadLetterDestination = new Destination { Name = "orders-dlq" }
    };
});
```

### Handler Concurrency

```csharp
messaging.RegisterHandler<OrderPlacedHandler, OrderPlaced>(opts =>
{
    opts.MaxConcurrency = 5;  // Process up to 5 messages concurrently
});
```

### Transport-Scoped Handler

```csharp
messaging.RegisterHandler<KafkaOnlyHandler, OrderPlaced>(opts =>
{
    opts.TransportScope = "kafka";  // Only receive from Kafka, not RabbitMQ
});
```

### Custom Default Serializer

```csharp
messaging.UseDefaultSerializer(new ProtobufMessageSerializer());
```

---

## Environment Variables

Adapter options support environment variable defaults:

| Variable | Maps to | Default |
|----------|---------|---------|
| `MESSAGGERO_KAFKA_BOOTSTRAP_SERVERS` | `KafkaOptions.BootstrapServers` | `localhost:9092` |
| `MESSAGGERO_KAFKA_GROUP_ID` | `KafkaOptions.GroupId` | `messaggero-default` |
| `MESSAGGERO_RABBITMQ_HOST` | `RabbitMqOptions.HostName` | `localhost` |
| `MESSAGGERO_RABBITMQ_PORT` | `RabbitMqOptions.Port` | `5672` |

Fluent builder values take precedence over environment variables.
