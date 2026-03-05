using Testcontainers.Kafka;

namespace Messaggero.Tests.Integration.Fixtures;

public sealed class KafkaFixture : IAsyncLifetime
{
    private readonly KafkaContainer _container = new KafkaBuilder("confluentinc/cp-kafka:7.6.0")
        .Build();

    public string BootstrapServers => _container.GetBootstrapAddress();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition("Kafka")]
public class KafkaCollection : ICollectionFixture<KafkaFixture>
{
}
