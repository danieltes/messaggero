# Messaggero

A broker-agnostic messaging library for .NET with multi-transport routing, fan-out/fan-in delivery, retry policies, and OpenTelemetry observability.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Target Framework](https://img.shields.io/badge/.NET-10.0-purple)](https://dotnet.microsoft.com/)
![Build Status](https://github.com/danieltes/messaggero/actions/workflows/dotnet.yml/badge.svg)

![Messaggero Logo](messaggero_logo_200x200.png)

## Overview

Messaggero decouples application code from broker-specific APIs by exposing a single stable interface for publishing and consuming messages. Multiple transport adapters (Kafka, RabbitMQ) can be active simultaneously within the same process. A message-type-based routing layer decides which transport handles each message — no broker logic leaks into application code.

- Single `IMessageBus.PublishAsync` call regardless of broker
- Swappable, simultaneously active transports
- Configuration-driven routing (including fan-out to multiple transports)
- Class-based handlers with optional lifecycle hooks
- Built-in retry policies with fixed or exponential backoff
- OpenTelemetry integration via `EnableObservability()`
- In-memory test double (`Messaggero.Testing`) for unit and integration tests

## Packages

| Package | Description |
|---|---|
| `Messaggero` | Core library — abstractions, routing, hosting, DI registration |
| `Messaggero.Kafka` | Apache Kafka transport adapter (at-least-once, `acks=all`) |
| `Messaggero.RabbitMQ` | RabbitMQ transport adapter (at-least-once, publisher confirms) |
| `Messaggero.Testing` | In-memory adapter and `TestMessageBus` assertion helper |

## Installation

```shell
dotnet add package Messaggero
dotnet add package Messaggero.Kafka        # optional
dotnet add package Messaggero.RabbitMQ     # optional
dotnet add package Messaggero.Testing      # test projects only
```

## Quick Start

### 1. Define a message

```csharp
public sealed class OrderPlaced
{
    public required string OrderId { get; init; }
    public required decimal Total   { get; init; }
}
```

### 2. Implement a handler

```csharp
public sealed class OrderPlacedHandler : IMessageHandler<OrderPlaced>
{
    public Task HandleAsync(
        OrderPlaced message,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Order {message.OrderId} via {context.SourceTransport} " +
                          $"(attempt {context.DeliveryAttempt})");
        return Task.CompletedTask;
    }
}
```

### 3. Register with the DI container

```csharp
builder.Services.AddMessaggero(messaging =>
{
    messaging
        .AddKafka("kafka", kafka =>
        {
            kafka.BootstrapServers = "localhost:9092";
            kafka.GroupId          = "my-service";
        })
        .Route<OrderPlaced>(r => r.ToTransport("kafka"))
        .RegisterHandler<OrderPlacedHandler, OrderPlaced>()
        .EnableObservability();
});
```

### 4. Publish a message

```csharp
public class OrderService(IMessageBus bus)
{
    public async Task PlaceOrderAsync(string orderId, decimal total)
    {
        var result = await bus.PublishAsync(new OrderPlaced
        {
            OrderId = orderId,
            Total   = total,
        });

        if (!result.IsSuccess)
            throw new InvalidOperationException($"Publish failed: {result}");
    }
}
```

---

## Configuration Reference

### `AddMessaggero`

```csharp
services.AddMessaggero(messaging =>
{
    messaging
        // ── transports ────────────────────────────────────────────
        .AddKafka("kafka",    kafka    => { ... })
        .AddRabbitMQ("rmq",   rabbit   => { ... })

        // ── routing ───────────────────────────────────────────────
        .Route<OrderPlaced>   (r => r.ToTransport("kafka"))
        .Route<EmailRequested>(r => r.ToTransport("rmq"))
        .Route<AuditEvent>    (r => r.ToTransport("kafka").ToTransport("rmq")) // fan-out

        // ── handlers ─────────────────────────────────────────────
        .RegisterHandler<OrderPlacedHandler, OrderPlaced>()
        .RegisterHandler<EmailHandler, EmailRequested>(opts =>
        {
            opts.MaxConcurrency = 4;
        })

        // ── serialization / observability ─────────────────────────
        .UseDefaultSerializer(new ProtobufMessageSerializer())
        .EnableObservability();
});
```

### Transport adapters

#### Kafka (`Messaggero.Kafka`)

```csharp
.AddKafka("kafka", kafka =>
{
    kafka.BootstrapServers = "localhost:9092";   // default: $MESSAGGERO_KAFKA_BOOTSTRAP_SERVERS
    kafka.GroupId          = "my-service";       // default: $MESSAGGERO_KAFKA_GROUP_ID
    kafka.PrefetchCount    = 100;                // messages buffered per consumer

    // Optional per-adapter retry policy
    kafka.RetryPolicy = new RetryPolicyOptions
    {
        MaxAttempts          = 5,
        BackoffStrategy      = BackoffStrategy.Exponential,
        InitialDelay         = TimeSpan.FromSeconds(1),
        MaxDelay             = TimeSpan.FromSeconds(30),
        DeadLetterDestination = new Destination { Name = "orders-dlq" },
    };

    // Pass any Confluent.Kafka producer/consumer config directly
    kafka.ProducerConfig["socket.keepalive.enable"] = "true";
    kafka.ConsumerConfig["fetch.min.bytes"]          = "1024";
})
```

| Property | Default | Description |
|---|---|---|
| `BootstrapServers` | `localhost:9092` | Comma-separated `host:port` list |
| `GroupId` | `messaggero-default` | Consumer group identifier |
| `PrefetchCount` | adapter default | Max in-flight messages per consumer |
| `ProducerConfig` | `{}` | Raw Confluent.Kafka producer properties |
| `ConsumerConfig` | `{}` | Raw Confluent.Kafka consumer properties |

Environment variables: `MESSAGGERO_KAFKA_BOOTSTRAP_SERVERS`, `MESSAGGERO_KAFKA_GROUP_ID`

#### RabbitMQ (`Messaggero.RabbitMQ`)

```csharp
.AddRabbitMQ("rmq", rabbit =>
{
    rabbit.HostName    = "localhost";  // default: $MESSAGGERO_RABBITMQ_HOST
    rabbit.Port        = 5672;         // default: $MESSAGGERO_RABBITMQ_PORT
    rabbit.UserName    = "guest";
    rabbit.Password    = "guest";
    rabbit.VirtualHost = "/";
    rabbit.AutomaticRecoveryEnabled = true;
    rabbit.PrefetchCount = 50;
})
```

| Property | Default | Description |
|---|---|---|
| `HostName` | `localhost` | Broker hostname |
| `Port` | `5672` | AMQP port |
| `UserName` | `guest` | Credentials |
| `Password` | `guest` | Credentials |
| `VirtualHost` | `/` | Virtual host |
| `AutomaticRecoveryEnabled` | `true` | Reconnect on connection drop |
| `PrefetchCount` | adapter default | `BasicQos` prefetch count |

Environment variables: `MESSAGGERO_RABBITMQ_HOST`, `MESSAGGERO_RABBITMQ_PORT`

### Routing

By default the destination name is the message type name lowercased (e.g. `OrderPlaced` → `orderplaced`).

```csharp
// Custom topic / queue name
.Route<OrderPlaced>(r => r.ToTransport("kafka").ToDestination("orders-v2"))

// RabbitMQ exchange with routing key override
.Route<OrderPlaced>(r => r
    .ToTransport("rmq")
    .ToDestination("events", new Dictionary<string, string>
    {
        ["routingKey"] = "orders.placed",
    }))

// Fan-out — delivered to both transports
.Route<AuditEvent>(r => r.ToTransport("kafka").ToTransport("rmq"))
```

### Handlers

```csharp
// Sequential delivery (default)
.RegisterHandler<OrderPlacedHandler, OrderPlaced>()

// Concurrent delivery — up to 5 messages processed in parallel
.RegisterHandler<OrderPlacedHandler, OrderPlaced>(opts =>
{
    opts.MaxConcurrency = 5;
})

// Transport-scoped — only receives messages from a specific adapter
.RegisterHandler<KafkaAuditHandler, AuditEvent>(opts =>
{
    opts.TransportScope = "kafka";
})
```

**`HandlerOptions`**

| Property | Default | Description |
|---|---|---|
| `MaxConcurrency` | `1` | Max concurrent handler invocations |
| `TransportScope` | `null` (all) | Restrict handler to one named transport |

### Optional lifecycle hooks

Handlers can implement `IHandlerLifecycle` to run setup and teardown logic:

```csharp
public sealed class OrderPlacedHandler : IMessageHandler<OrderPlaced>, IHandlerLifecycle
{
    public Task InitializeAsync(CancellationToken cancellationToken) { /* warm-up */ return Task.CompletedTask; }
    public Task DisposeAsync() { /* clean-up */  return Task.CompletedTask; }

    public Task HandleAsync(OrderPlaced message, MessageContext context, CancellationToken ct)
        => Task.CompletedTask;
}
```

### `MessageContext`

Every handler receives a `MessageContext` alongside the typed message:

| Property | Type | Description |
|---|---|---|
| `MessageId` | `string` | Unique message identifier |
| `MessageType` | `string` | CLR type name of the message |
| `SourceTransport` | `string` | Name of the adapter that delivered the message |
| `Headers` | `MessageHeaders` | Arbitrary key-value metadata |
| `Timestamp` | `DateTimeOffset` | Time the message was produced |
| `DeliveryAttempt` | `int` | `1` on first delivery; incremented on retries |

### Retry policy

```csharp
kafka.RetryPolicy = new RetryPolicyOptions
{
    MaxAttempts           = 5,
    BackoffStrategy       = BackoffStrategy.Exponential, // Fixed | Exponential
    InitialDelay          = TimeSpan.FromSeconds(1),
    MaxDelay              = TimeSpan.FromSeconds(30),
    DeadLetterDestination = new Destination { Name = "my-dlq" },
};
```

| Strategy | Behaviour |
|---|---|
| `Fixed` | Constant delay between every retry |
| `Exponential` | Delay doubles each attempt, capped at `MaxDelay` |

### Custom serializer

The default serializer uses `System.Text.Json`. Swap it globally:

```csharp
.UseDefaultSerializer(new ProtobufMessageSerializer())
```

Implement `IMessageSerializer` to integrate any serialization library:

```csharp
public interface IMessageSerializer
{
    string    ContentType { get; }
    byte[]    Serialize<TMessage>(TMessage message) where TMessage : class;
    TMessage  Deserialize<TMessage>(ReadOnlySpan<byte> data) where TMessage : class;
}
```

### Observability

```csharp
.EnableObservability()
```

Enables OpenTelemetry tracing and metrics. Instruments publish and consume operations with activity spans and counters compatible with any OpenTelemetry-compatible backend (Jaeger, Prometheus, Azure Monitor, etc.).

### Custom headers

```csharp
var headers = new MessageHeaders();
headers["correlation-id"] = correlationId;
headers["tenant-id"]      = tenantId;

await bus.PublishAsync(message, headers);
```

Headers are forwarded to the broker and available in `MessageContext.Headers` on the consumer side.

---

## Multiple Transports Example

```csharp
builder.Services.AddMessaggero(messaging =>
{
    messaging
        .AddKafka("kafka", kafka =>
        {
            kafka.BootstrapServers = "kafka:9092";
            kafka.GroupId          = "order-service";
        })
        .AddRabbitMQ("rmq", rabbit =>
        {
            rabbit.HostName = "rabbitmq";
            rabbit.UserName = "app";
            rabbit.Password = "secret";
        })
        // Each message type routed to the appropriate broker
        .Route<OrderPlaced>   (r => r.ToTransport("kafka"))
        .Route<EmailRequested>(r => r.ToTransport("rmq"))
        // Fan-out: audit events sent to both brokers
        .Route<AuditEvent>    (r => r.ToTransport("kafka").ToTransport("rmq"))
        // Handlers
        .RegisterHandler<OrderPlacedHandler,    OrderPlaced>   (opts => opts.MaxConcurrency = 4)
        .RegisterHandler<EmailHandler,          EmailRequested>()
        .RegisterHandler<KafkaAuditHandler,     AuditEvent>    (opts => opts.TransportScope = "kafka")
        .RegisterHandler<RabbitMqAuditHandler,  AuditEvent>    (opts => opts.TransportScope = "rmq")
        .EnableObservability();
});
```

---

## Testing

Add `Messaggero.Testing` to your test project.

### `TestMessageBus` — lightweight publish assertions

Use `TestMessageBus` when you only need to verify that the correct messages are published from a unit:

```csharp
[Fact]
public async Task PlaceOrder_PublishesOrderPlacedEvent()
{
    var bus   = new TestMessageBus();
    var svc   = new OrderService(bus);

    await svc.PlaceOrderAsync("ORD-1", 99.99m);

    bus.AssertPublished<OrderPlaced>();

    var events = bus.GetPublishedMessages<OrderPlaced>();
    events.Should().ContainSingle(e => e.OrderId == "ORD-1");
}
```

`TestMessageBus` API:

| Method | Description |
|---|---|
| `AssertPublished<T>()` | Throws if no message of type `T` was published |
| `GetPublishedMessages<T>()` | Returns all published messages of type `T` |
| `GetAllPublished()` | Returns all published messages with their type and headers |
| `Reset()` | Clears the captured publish list |

### In-memory transport — full pipeline tests

Use `AddInMemory` to run the full Messaggero pipeline in-process without a real broker:

```csharp
[Fact]
public async Task Handler_IsInvokedWhenMessagePublished()
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddMessaggero(messaging =>
    {
        messaging
            .AddInMemory("mem")
            .Route<OrderPlaced>(r => r.ToTransport("mem"))
            .RegisterHandler<OrderPlacedHandler, OrderPlaced>();
    });

    await using var provider = services.BuildServiceProvider();
    var host = provider.GetRequiredService<MessagingHost>();
    await host.StartAsync(CancellationToken.None);

    var bus = provider.GetRequiredService<IMessageBus>();
    await bus.PublishAsync(new OrderPlaced { OrderId = "ORD-42", Total = 50m });

    // assert via your handler's captured state, or inspect the adapter directly
}
```

### Handler isolation — no library host needed

```csharp
[Fact]
public async Task Handler_CanBeTestedInIsolation()
{
    var handler = new OrderPlacedHandler();
    var context = new MessageContext
    {
        MessageId       = "test-1",
        MessageType     = nameof(OrderPlaced),
        SourceTransport = "test",
        Headers         = new MessageHeaders(),
        Timestamp       = DateTimeOffset.UtcNow,
        DeliveryAttempt = 1,
    };

    await handler.HandleAsync(
        new OrderPlaced { OrderId = "ORD-42", Total = 99.99m },
        context,
        CancellationToken.None);

    // assert handler side-effects directly
}
```

---

## Delivery Semantics

| Adapter | Guarantee | Ordering | Ack model | Nack behaviour |
|---|---|---|---|---|
| Kafka | At-least-once | Per-partition (key = message type) | Manual offset commit | No-op — replayed on consumer restart |
| RabbitMQ | At-least-once | Per-queue FIFO | `BasicAck` per delivery tag | `BasicNack(requeue: false)` → DLX |
| InMemory | At-most-once | FIFO per destination | Remove from pending set | Move to `DeadLetterMessages` list |

For more detail see [docs/adapter-semantics.md](docs/adapter-semantics.md).

---

## Building from Source

**Prerequisites**: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Restore dependencies

```shell
dotnet restore
```

### Build

```shell
dotnet build
```

### Run tests

```shell
# Unit tests
dotnet test tests/Messaggero.Tests.Unit

# Contract tests
dotnet test tests/Messaggero.Tests.Contract

# Integration tests
dotnet test tests/Messaggero.Tests.Integration

# All tests
dotnet test
```

### Run benchmarks

```shell
dotnet run --project tests/Messaggero.Tests.Benchmarks -c Release
```

### Pack NuGet packages

```shell
dotnet pack -c Release
```

Output packages are written to each project's `bin/Release/` folder.
