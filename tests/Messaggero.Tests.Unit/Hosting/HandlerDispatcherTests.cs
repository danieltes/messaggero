using FluentAssertions;
using Messaggero.Abstractions;
using Messaggero.Configuration;
using Messaggero.Hosting;
using Messaggero.Model;
using Messaggero.Routing;
using Messaggero.Serialization;
using Messaggero.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Messaggero.Tests.Unit.Hosting;

public class HandlerDispatcherTests
{
    private sealed record TestEvent(string Value);

    private sealed class TestHandler : IMessageHandler<TestEvent>
    {
        public List<(TestEvent Event, MessageContext Context)> Received { get; } = [];
        private readonly SemaphoreSlim _signal = new(0);

        public Task HandleAsync(TestEvent message, MessageContext context, CancellationToken cancellationToken)
        {
            Received.Add((message, context));
            _signal.Release();
            return Task.CompletedTask;
        }

        public Task WaitForMessage(TimeSpan timeout) => _signal.WaitAsync(timeout);
    }

    private sealed class FailingHandler : IMessageHandler<TestEvent>
    {
        public int Attempts { get; private set; }

        public Task HandleAsync(TestEvent message, MessageContext context, CancellationToken cancellationToken)
        {
            Attempts++;
            throw new InvalidOperationException("Handler failure");
        }
    }

    private (HandlerDispatcher Dispatcher, InMemoryTransportAdapter Adapter, TestHandler Handler) CreateSetup(
        int maxConcurrency = 1, int prefetchCount = 100, int maxRetryAttempts = 1)
    {
        var adapter = new InMemoryTransportAdapter("test-transport");
        var handler = new TestHandler();

        var services = new ServiceCollection();
        services.AddSingleton<TestHandler>(handler);
        var sp = services.BuildServiceProvider();

        var config = new MessagingConfiguration
        {
            RoutingTable = new RoutingTable(
            [
                new RoutingRule { MessageType = nameof(TestEvent), Transports = ["test-transport"] }
            ]),
            Transports = new Dictionary<string, TransportRegistration>
            {
                ["test-transport"] = new TransportRegistration
                {
                    Name = "test-transport",
                    AdapterFactory = _ => adapter,
                    Options = new TransportOptions
                    {
                        PrefetchCount = prefetchCount,
                        RetryPolicy = new RetryPolicyOptions
                        {
                            MaxAttempts = maxRetryAttempts,
                            BackoffStrategy = BackoffStrategy.Fixed,
                            InitialDelay = TimeSpan.FromMilliseconds(1)
                        }
                    }
                }
            },
            Handlers =
            [
                new HandlerRegistration
                {
                    MessageType = nameof(TestEvent),
                    HandlerType = typeof(TestHandler),
                    MessageClrType = typeof(TestEvent),
                    MaxConcurrency = maxConcurrency
                }
            ],
            DefaultSerializer = new JsonMessageSerializer()
        };

        var adapters = new Dictionary<string, ITransportAdapter> { ["test-transport"] = adapter };
        var dispatcher = new HandlerDispatcher(config, adapters, sp, NullLogger.Instance);

        return (dispatcher, adapter, handler);
    }

    [Fact]
    public async Task StartAsync_SubscribesToAdapter_DispatchesToHandler()
    {
        var (dispatcher, adapter, handler) = CreateSetup();
        await adapter.StartAsync(CancellationToken.None);
        await dispatcher.StartAsync(CancellationToken.None);

        var serializer = new JsonMessageSerializer();
        var payload = serializer.Serialize(new TestEvent("hello"));
        var message = new Message
        {
            Id = "msg-1",
            Type = "TestEvent",
            Payload = payload,
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow,
            SourceTransport = "test-transport"
        };

        var destination = new Destination { Name = "testevent" };
        await adapter.PublishAsync(message, destination, CancellationToken.None);

        // Wait for async dispatch
        await handler.WaitForMessage(TimeSpan.FromSeconds(5));

        handler.Received.Should().ContainSingle();
        handler.Received[0].Event.Value.Should().Be("hello");
        handler.Received[0].Context.SourceTransport.Should().Be("test-transport");

        await dispatcher.StopAsync(CancellationToken.None);
        await adapter.DisposeAsync();
    }

    [Fact]
    public async Task Dispatch_AcknowledgesOnSuccess()
    {
        var (dispatcher, adapter, handler) = CreateSetup();
        await adapter.StartAsync(CancellationToken.None);
        await dispatcher.StartAsync(CancellationToken.None);

        var serializer = new JsonMessageSerializer();
        var payload = serializer.Serialize(new TestEvent("ack-test"));
        var message = new Message
        {
            Id = "msg-ack",
            Type = "TestEvent",
            Payload = payload,
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow,
            SourceTransport = "test-transport"
        };

        var destination = new Destination { Name = "testevent" };
        await adapter.PublishAsync(message, destination, CancellationToken.None);
        await handler.WaitForMessage(TimeSpan.FromSeconds(5));

        // After successful handling, message should be acknowledged (removed from pending)
        await Task.Delay(100); // Allow async ack to complete
        adapter.PendingMessages.Should().BeEmpty();

        await dispatcher.StopAsync(CancellationToken.None);
        await adapter.DisposeAsync();
    }

    [Fact]
    public async Task Dispatch_RejectsOnFailure()
    {
        var failHandler = new FailingHandler();
        var adapter = new InMemoryTransportAdapter("test-transport");

        var services = new ServiceCollection();
        services.AddSingleton<FailingHandler>(failHandler);
        var sp = services.BuildServiceProvider();

        var config = new MessagingConfiguration
        {
            RoutingTable = new RoutingTable(
            [
                new RoutingRule { MessageType = nameof(TestEvent), Transports = ["test-transport"] }
            ]),
            Transports = new Dictionary<string, TransportRegistration>
            {
                ["test-transport"] = new TransportRegistration
                {
                    Name = "test-transport",
                    AdapterFactory = _ => adapter,
                    Options = new TransportOptions
                    {
                        RetryPolicy = new RetryPolicyOptions
                        {
                            MaxAttempts = 1,
                            BackoffStrategy = BackoffStrategy.Fixed,
                            InitialDelay = TimeSpan.FromMilliseconds(1)
                        }
                    }
                }
            },
            Handlers =
            [
                new HandlerRegistration
                {
                    MessageType = nameof(TestEvent),
                    HandlerType = typeof(FailingHandler),
                    MessageClrType = typeof(TestEvent),
                    MaxConcurrency = 1
                }
            ],
            DefaultSerializer = new JsonMessageSerializer()
        };

        var adapters = new Dictionary<string, ITransportAdapter> { ["test-transport"] = adapter };
        var dispatcher = new HandlerDispatcher(config, adapters, sp, NullLogger.Instance);

        await adapter.StartAsync(CancellationToken.None);
        await dispatcher.StartAsync(CancellationToken.None);

        var serializer = new JsonMessageSerializer();
        var payload = serializer.Serialize(new TestEvent("fail-test"));
        var message = new Message
        {
            Id = "msg-fail",
            Type = "TestEvent",
            Payload = payload,
            Headers = new MessageHeaders(),
            Timestamp = DateTimeOffset.UtcNow,
            SourceTransport = "test-transport"
        };

        var destination = new Destination { Name = "testevent" };
        await adapter.PublishAsync(message, destination, CancellationToken.None);

        // Wait for dispatch + reject
        await Task.Delay(500);

        adapter.DeadLetterMessages.Should().ContainSingle()
            .Which.Id.Should().Be("msg-fail");

        await dispatcher.StopAsync(CancellationToken.None);
        await adapter.DisposeAsync();
    }
}
