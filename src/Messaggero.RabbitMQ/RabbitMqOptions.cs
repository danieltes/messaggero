namespace Messaggero.RabbitMQ;

/// <summary>
/// RabbitMQ-specific transport options.
/// </summary>
public sealed class RabbitMqOptions : Configuration.TransportOptions
{
    /// <summary>RabbitMQ host name. Falls back to MESSAGGERO_RABBITMQ_HOST env var.</summary>
    public string HostName { get; set; } =
        Environment.GetEnvironmentVariable("MESSAGGERO_RABBITMQ_HOST") ?? "localhost";

    /// <summary>RabbitMQ port. Falls back to MESSAGGERO_RABBITMQ_PORT env var.</summary>
    public int Port { get; set; } =
        int.TryParse(Environment.GetEnvironmentVariable("MESSAGGERO_RABBITMQ_PORT"), out var p) ? p : 5672;

    /// <summary>Username.</summary>
    public string UserName { get; set; } = "guest";

    /// <summary>Password.</summary>
    public string Password { get; set; } = "guest";

    /// <summary>Virtual host.</summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>Enable automatic connection recovery.</summary>
    public bool AutomaticRecoveryEnabled { get; set; } = true;
}
