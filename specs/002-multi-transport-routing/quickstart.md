# Quickstart: Multi-Transport Routing

**Feature**: 002-multi-transport-routing  
**Date**: 2026-03-04

## Prerequisites

- .NET 10.0 SDK
- Running RabbitMQ instance (e.g., `docker run -d -p 5672:5672 rabbitmq:4`)
- Running Kafka instance (e.g., `docker run -d -p 9092:9092 apache/kafka:latest`)

## 1. Configure Multiple Transports

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMessaggero(bus => bus
    // Register both transports
    .AddRabbitMq(config => config.HostName = "localhost")
    .AddKafka(config => config.BootstrapServers = "localhost:9092")
    
    // Route by destination
    .RouteDestination("orders.*", "Kafka")
    .RouteDestination("notifications.*", "RabbitMQ")
    
    // Everything else goes to RabbitMQ
    .UseDefaultTransport("RabbitMQ"));

var app = builder.Build();
```

## 2. Publish Messages (unchanged API)

```csharp
app.MapPost("/order", async (IMessageBus bus) =>
{
    // Automatically routed to Kafka (matches "orders.*")
    await bus.PublishAsync("orders.created", new OrderCreated(Id: 42));
    
    // Automatically routed to RabbitMQ (matches "notifications.*")
    await bus.PublishAsync("notifications.email", new SendEmail(To: "user@example.com"));
    
    return Results.Ok();
});
```

## 3. Subscribe Across Transports

```csharp
// Subscribe on Kafka (resolved via same routing rules)
await bus.SubscribeAsync<OrderCreated>(
    "orders.created", 
    "order-processor", 
    new OrderHandler());

// Subscribe on RabbitMQ
await bus.SubscribeAsync<SendEmail>(
    "notifications.email", 
    "email-sender", 
    new EmailHandler());
```

## 4. Health Check

```csharp
var health = await bus.CheckHealthAsync();

// Aggregate status
Console.WriteLine($"Overall: {health.Status}");

// Per-transport status
foreach (var entry in health.TransportEntries)
{
    Console.WriteLine($"  {entry.TransportName}: {entry.Status}");
}
// Output:
//   Overall: Healthy
//   Kafka: Healthy
//   RabbitMQ: Healthy
```

## 5. Backward Compatible (Single Transport)

Existing single-transport code requires **zero changes**:

```csharp
// This still works exactly as before
builder.Services.AddMessaggero(bus => bus
    .UseRabbitMq(config => config.HostName = "localhost"));
```

## Common Patterns

### Type-Based Routing

```csharp
builder.Services.AddMessaggero(bus => bus
    .AddRabbitMq(config => config.HostName = "localhost")
    .AddKafka(config => config.BootstrapServers = "localhost:9092")
    .RouteType<OrderEvent>("Kafka")          // All OrderEvent subclasses → Kafka
    .RouteType<SendEmailCommand>("RabbitMQ") // Commands → RabbitMQ
    .UseDefaultTransport("RabbitMQ"));
```

### Named Transports

```csharp
builder.Services.AddMessaggero(bus => bus
    .AddKafka("primary-kafka", config => config.BootstrapServers = "kafka1:9092")
    .AddKafka("archive-kafka", config => config.BootstrapServers = "kafka2:9092")
    .RouteDestination("events.*", "primary-kafka")
    .RouteDestination("archive.*", "archive-kafka"));
```
