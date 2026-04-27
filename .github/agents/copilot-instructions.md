# messaggero Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-04-26

## Active Technologies
- C# / .NET 10.0 (`net10.0`, `LangVersion` latest) + xunit 2.9.3, NSubstitute 5.3.0, FluentAssertions 8.3.0 → Assertivo 0.1.2, Microsoft.Extensions.* 10.0.0-preview.3 (00003-fluentassertions-to-assertivo)
- C# / .NET 10 (`net10.0`) + xunit.v3 3.2.2, Microsoft.Extensions.* 10.0.7, Confluent.Kafka 2.14.0, RabbitMQ.Client 7.2.1, Testcontainers 4.11.0, BenchmarkDotNet 0.15.8 (00012-upgrade-xunit3-deps)

- C# latest (C# 13) / .NET 10 + Confluent.Kafka (Kafka adapter), RabbitMQ.Client (RabbitMQ adapter), Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options, System.Text.Json (default serializer) (00001-broker-agnostic-core)

## Project Structure

```text
src/
tests/
```

## Commands

# Add commands for C# latest (C# 13) / .NET 10

## Code Style

C# latest (C# 13) / .NET 10: Follow standard conventions

## Recent Changes
- 00012-upgrade-xunit3-deps: Added C# / .NET 10 (`net10.0`) + xunit.v3 3.2.2, Microsoft.Extensions.* 10.0.7, Confluent.Kafka 2.14.0, RabbitMQ.Client 7.2.1, Testcontainers 4.11.0, BenchmarkDotNet 0.15.8
- 00003-fluentassertions-to-assertivo: Added C# / .NET 10.0 (`net10.0`, `LangVersion` latest) + xunit 2.9.3, NSubstitute 5.3.0, FluentAssertions 8.3.0 → Assertivo 0.1.2, Microsoft.Extensions.* 10.0.0-preview.3

- 00001-broker-agnostic-core: Added C# latest (C# 13) / .NET 10 + Confluent.Kafka (Kafka adapter), RabbitMQ.Client (RabbitMQ adapter), Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Options, System.Text.Json (default serializer)

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
