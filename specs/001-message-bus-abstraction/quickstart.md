# Quickstart: Message Bus Abstraction Library

**Feature**: `001-message-bus-abstraction`  
**Date**: 2026-03-04

## Prerequisites

- .NET 10 SDK
- A running RabbitMQ or Kafka broker (or Docker for local development)

## 1. Install NuGet Packages

```shell
# Core + RabbitMQ transport
dotnet add package Messaggero
dotnet add package Messaggero.Transport.RabbitMQ

# Or: Core + Kafka transport
dotnet add package Messaggero
dotnet add package Messaggero.Transport.Kafka
```

## 2. Configure the Message Bus

### Using RabbitMQ

```csharp
using Messaggero;
using Messaggero.Transport.RabbitMQ;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMessaggero(bus => bus
    .UseRabbitMq(rabbit =>
    {
        rabbit.HostName = "localhost";
        rabbit.Port = 5672;
        rabbit.UserName = "guest";
        rabbit.Password = "guest";
    })
);

var app = builder.Build();
await app.RunAsync();
```

### Using Kafka

```csharp
using Messaggero;
using Messaggero.Transport.Kafka;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMessaggero(bus => bus
    .UseKafka(kafka =>
    {
        kafka.BootstrapServers = "localhost:9092";
    })
);

var app = builder.Build();
await app.RunAsync();
```

### Switching transports

Change only the DI registration — no other code changes needed:

```csharp
// Before: RabbitMQ
bus.UseRabbitMq(rabbit => { rabbit.HostName = "localhost"; });

// After: Kafka
bus.UseKafka(kafka => { kafka.BootstrapServers = "localhost:9092"; });
```

All `PublishAsync` and `SubscribeAsync` calls remain identical.

## 3. Publish a Message

```csharp
public class OrderCreatedEvent
{
    public required Guid OrderId { get; init; }
    public required decimal Total { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public class OrderService(IMessageBus bus)
{
    public async Task CreateOrderAsync(Order order)
    {
        // ... save order to database ...

        await bus.PublishAsync(
            destination: "orders.created",
            payload: new OrderCreatedEvent
            {
                OrderId = order.Id,
                Total = order.Total,
                CreatedAt = DateTimeOffset.UtcNow
            },
            options: new MessagePublishOptions
            {
                RoutingKey = order.Id.ToString() // per-order ordering
            }
        );
    }
}
```

## 4. Subscribe to Messages

```csharp
public class OrderCreatedHandler : IMessageHandler<OrderCreatedEvent>
{
    public Task HandleAsync(
        MessageEnvelope<OrderCreatedEvent> envelope,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine(
            $"Order {envelope.Payload.OrderId} created " +
            $"for ${envelope.Payload.Total} " +
            $"[msgId={envelope.MessageId}]");

        return Task.CompletedTask;
    }
}

// Register subscription at startup:
var bus = app.Services.GetRequiredService<IMessageBus>();

await bus.SubscribeAsync<OrderCreatedEvent>(
    destination: "orders.created",
    groupId: "notification-service",
    handler: new OrderCreatedHandler(),
    options: new SubscriptionOptions
    {
        MaxConcurrency = 4,           // 4 messages in parallel, per-key ordering preserved
        ErrorStrategy = ErrorStrategy.Retry(maxRetries: 3)
    }
);
```

## 5. Observe Lifecycle Events

```csharp
var bus = app.Services.GetRequiredService<IMessageBus>();

bus.OnLifecycleEvent(evt =>
{
    Console.WriteLine($"[{evt.Timestamp:HH:mm:ss}] {evt.EventType} " +
                      $"transport={evt.TransportName} " +
                      $"destination={evt.Destination} " +
                      $"messageId={evt.MessageId}");
});
```

## 6. Health Checks

```csharp
var result = await bus.CheckHealthAsync();

if (result.IsHealthy)
    Console.WriteLine($"Transport '{result.TransportName}' is healthy");
else
    Console.WriteLine($"Transport unhealthy: {result.Description}");
```

## 7. Graceful Shutdown

The library shuts down gracefully when the host stops — in-flight messages
complete processing before the broker connection is closed. No additional
code is required when using `IHost`.

For manual control:

```csharp
await bus.DisposeAsync(); // finishes in-flight, then disconnects
```

## Validation Checklist

After setup, verify end-to-end:

1. Start the application with a running broker.
2. Publish a message via `PublishAsync`.
3. Confirm the subscriber handler receives the `MessageEnvelope`.
4. Stop the application — confirm graceful shutdown (no lost messages).
5. Change the transport configuration (RabbitMQ ↔ Kafka) — confirm identical behavior.
