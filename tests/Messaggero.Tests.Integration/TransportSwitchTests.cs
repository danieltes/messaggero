using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Serialization;
using Messaggero.Tests.Integration.Fixtures;
using Messaggero.Transport.Kafka;
using Messaggero.Transport.RabbitMQ;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Messaggero.Tests.Integration;

/// <summary>
/// Tests that verify identical behavior when switching transports.
/// The same test logic is run with both RabbitMQ and Kafka transports.
/// </summary>
public class TransportSwitchTests
{
    [Fact]
    public void AddMessaggero_WithoutTransport_ThrowsActionableError()
    {
        var services = new ServiceCollection();

        var act = () => services.AddMessaggero(_ => { });

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*No transport configured*")
           .WithMessage("*UseRabbitMq*");
    }

    [Fact]
    public void AddMessaggero_WithRabbitMq_InvalidHostName_ThrowsFast()
    {
        var services = new ServiceCollection();

        var act = () => services.AddMessaggero(bus =>
            bus.UseRabbitMq(config => config.HostName = ""));

        act.Should().Throw<ArgumentException>()
           .WithMessage("*HostName*");
    }

    [Fact]
    public void AddMessaggero_WithRabbitMq_InvalidPort_ThrowsFast()
    {
        var services = new ServiceCollection();

        var act = () => services.AddMessaggero(bus =>
            bus.UseRabbitMq(config =>
            {
                config.HostName = "localhost";
                config.Port = 0;
            }));

        act.Should().Throw<ArgumentException>()
           .WithMessage("*Port*");
    }

    [Fact]
    public void AddMessaggero_WithKafka_EmptyBootstrapServers_ThrowsFast()
    {
        var services = new ServiceCollection();

        var act = () => services.AddMessaggero(bus =>
            bus.UseKafka(config => config.BootstrapServers = ""));

        act.Should().Throw<ArgumentException>()
           .WithMessage("*BootstrapServers*");
    }

    [Fact]
    public void Transport_RabbitMq_HasCorrectName()
    {
        var transport = new RabbitMqTransport(
            new RabbitMqConfiguration { HostName = "localhost" },
            NullLogger<RabbitMqTransport>.Instance);

        transport.Name.Should().Be("RabbitMQ");
    }

    [Fact]
    public void Transport_Kafka_HasCorrectName()
    {
        var transport = new KafkaTransport(
            new KafkaConfiguration { BootstrapServers = "localhost:9092" },
            NullLogger<KafkaTransport>.Instance);

        transport.Name.Should().Be("Kafka");
    }
}
