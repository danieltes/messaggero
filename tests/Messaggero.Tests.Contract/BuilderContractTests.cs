using FluentAssertions;
using Messaggero.Abstractions;
using NSubstitute;

namespace Messaggero.Tests.Contract;

/// <summary>
/// Contract tests verifying MessageBusBuilder multi-transport API validation rules.
/// </summary>
public class BuilderContractTests
{
    private static IMessageBusTransport CreateTransport(string name)
    {
        var transport = Substitute.For<IMessageBusTransport>();
        transport.Name.Returns(name);
        return transport;
    }

    [Fact]
    public void AddTransport_RegistersTransportByName()
    {
        var builder = new MessageBusBuilder();
        var transport = CreateTransport("Kafka");

        builder.AddTransport(transport);

        builder.TransportRegistry.Should().ContainKey("Kafka");
    }

    [Fact]
    public void AddTransport_WithName_RegistersByGivenName()
    {
        var builder = new MessageBusBuilder();
        var transport = CreateTransport("Kafka");

        builder.AddTransport("MyKafka", transport);

        builder.TransportRegistry.Should().ContainKey("MyKafka");
    }

    [Fact]
    public void AddTransport_DuplicateName_ThrowsDescriptively()
    {
        var builder = new MessageBusBuilder();
        builder.AddTransport(CreateTransport("Kafka"));

        var act = () => builder.AddTransport(CreateTransport("Kafka"));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'Kafka'*already registered*");
    }

    [Fact]
    public void RouteDestination_WithUnregisteredTransport_FailsAtValidation()
    {
        var builder = new MessageBusBuilder();
        builder.AddTransport(CreateTransport("RabbitMQ"));
        builder.RouteDestination("orders.*", "NonExistent");

        var act = () => builder.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'NonExistent'*not registered*");
    }

    [Fact]
    public void RouteDestination_ConflictingPatterns_FailsAtValidation()
    {
        var builder = new MessageBusBuilder();
        builder.AddTransport(CreateTransport("RabbitMQ"));
        builder.AddTransport(CreateTransport("Kafka"));
        builder.RouteDestination("orders.*", "Kafka");
        builder.RouteDestination("orders.*", "RabbitMQ");

        var act = () => builder.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Conflicting*orders.*");
    }

    [Fact]
    public void RouteType_WithUnregisteredTransport_FailsAtValidation()
    {
        var builder = new MessageBusBuilder();
        builder.AddTransport(CreateTransport("RabbitMQ"));
        builder.RouteType<string>("NonExistent");

        var act = () => builder.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'NonExistent'*not registered*");
    }

    [Fact]
    public void RouteType_ConflictingTypes_FailsAtValidation()
    {
        var builder = new MessageBusBuilder();
        builder.AddTransport(CreateTransport("RabbitMQ"));
        builder.AddTransport(CreateTransport("Kafka"));
        builder.RouteType<string>("Kafka");
        builder.RouteType<string>("RabbitMQ");

        var act = () => builder.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Conflicting*String*");
    }

    [Fact]
    public void UseDefaultTransport_WithUnregisteredName_FailsAtValidation()
    {
        var builder = new MessageBusBuilder();
        builder.AddTransport(CreateTransport("RabbitMQ"));
        builder.UseDefaultTransport("NonExistent");

        var act = () => builder.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*'NonExistent'*not registered*");
    }

    [Fact]
    public void UseTransport_RegistersAndSetsDefault()
    {
        var builder = new MessageBusBuilder();
        var transport = CreateTransport("RabbitMQ");

        builder.UseTransport(transport);

        builder.TransportRegistry.Should().ContainKey("RabbitMQ");
        builder.DefaultTransportName.Should().Be("RabbitMQ");
    }

    [Fact]
    public void Validate_NoTransports_ThrowsDescriptively()
    {
        var builder = new MessageBusBuilder();

        var act = () => builder.Validate();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*No transport configured*");
    }

    [Fact]
    public void Validate_ValidMultiTransportConfig_Succeeds()
    {
        var builder = new MessageBusBuilder();
        builder.AddTransport(CreateTransport("RabbitMQ"));
        builder.AddTransport(CreateTransport("Kafka"));
        builder.RouteDestination("orders.*", "Kafka");
        builder.RouteDestination("notifications.*", "RabbitMQ");
        builder.RouteType<int>("Kafka");
        builder.UseDefaultTransport("RabbitMQ");

        var act = () => builder.Validate();

        act.Should().NotThrow();
    }
}
