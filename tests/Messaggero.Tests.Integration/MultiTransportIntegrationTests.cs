using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Transport.Kafka;
using Messaggero.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;

namespace Messaggero.Tests.Integration;

/// <summary>
/// Integration tests for multi-transport routing configuration.
/// Validates DI registration, builder validation, and backward compatibility.
/// </summary>
public class MultiTransportIntegrationTests
{
    #region T033 — Multi-transport DI registration

    [Fact]
    public void AddMessaggero_MultiTransport_DestinationRouting_Resolves()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMessaggero(bus => bus
            .AddRabbitMq(config => config.HostName = "localhost")
            .AddKafka(config => config.BootstrapServers = "localhost:9092")
            .RouteDestination("orders.*", "Kafka")
            .RouteDestination("notifications.*", "RabbitMQ")
            .UseDefaultTransport("RabbitMQ"));

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();

        bus.Should().NotBeNull();
    }

    [Fact]
    public void AddMessaggero_MultiTransport_TypeRouting_Resolves()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMessaggero(bus => bus
            .AddRabbitMq(config => config.HostName = "localhost")
            .AddKafka(config => config.BootstrapServers = "localhost:9092")
            .RouteType<int>("Kafka")
            .RouteType<string>("RabbitMQ")
            .UseDefaultTransport("RabbitMQ"));

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();

        bus.Should().NotBeNull();
    }

    [Fact]
    public void AddMessaggero_MultiTransport_MixedRouting_Resolves()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMessaggero(bus => bus
            .AddRabbitMq(config => config.HostName = "localhost")
            .AddKafka(config => config.BootstrapServers = "localhost:9092")
            .RouteDestination("orders.*", "Kafka")
            .RouteType<string>("RabbitMQ")
            .UseDefaultTransport("RabbitMQ"));

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();

        bus.Should().NotBeNull();
    }

    [Fact]
    public void AddMessaggero_ConflictingRules_FailsFast()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var act = () => services.AddMessaggero(bus => bus
            .AddRabbitMq(config => config.HostName = "localhost")
            .AddKafka(config => config.BootstrapServers = "localhost:9092")
            .RouteDestination("orders.*", "Kafka")
            .RouteDestination("orders.*", "RabbitMQ"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Conflicting*orders.*");
    }

    [Fact]
    public void AddMessaggero_UnregisteredDefaultTransport_FailsFast()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var act = () => services.AddMessaggero(bus => bus
            .AddRabbitMq(config => config.HostName = "localhost")
            .UseDefaultTransport("NonExistent"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'NonExistent'*not registered*");
    }

    #endregion

    #region T034 — Backward compatibility

    [Fact]
    public void AddMessaggero_SingleTransport_UseRabbitMq_StillWorks()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMessaggero(bus =>
            bus.UseRabbitMq(config => config.HostName = "localhost"));

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();

        bus.Should().NotBeNull();
    }

    [Fact]
    public void AddMessaggero_SingleTransport_UseKafka_StillWorks()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMessaggero(bus =>
            bus.UseKafka(config => config.BootstrapServers = "localhost:9092"));

        var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();

        bus.Should().NotBeNull();
    }

    [Fact]
    public void AddMessaggero_NoTransport_ThrowsActionableError()
    {
        var services = new ServiceCollection();

        var act = () => services.AddMessaggero(_ => { });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No transport configured*");
    }

    #endregion
}
