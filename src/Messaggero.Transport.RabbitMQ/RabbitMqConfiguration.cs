namespace Messaggero.Transport.RabbitMQ;

/// <summary>
/// Configuration for the RabbitMQ transport.
/// </summary>
public sealed class RabbitMqConfiguration
{
    /// <summary>
    /// The RabbitMQ broker hostname. Required.
    /// </summary>
    public string HostName { get; set; } = string.Empty;

    /// <summary>
    /// The AMQP port. Default is 5672.
    /// </summary>
    public int Port { get; set; } = 5672;

    /// <summary>
    /// Authentication username. Default is "guest".
    /// </summary>
    public string UserName { get; set; } = "guest";

    /// <summary>
    /// Authentication password. Default is "guest".
    /// </summary>
    public string Password { get; set; } = "guest";

    /// <summary>
    /// RabbitMQ virtual host. Default is "/".
    /// </summary>
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Number of pooled publish channels. Default is 4. Must be ≥ 1.
    /// </summary>
    public int PublishChannelPoolSize { get; set; } = 4;

    /// <summary>
    /// TCP heartbeat interval for dead-connection detection. Default is 60 seconds.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Interval between auto-recovery retry attempts. Default is 5 seconds.
    /// </summary>
    public TimeSpan NetworkRecoveryInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Whether to use TLS for the connection. Default is false.
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Validates the configuration and throws if required fields are missing or invalid.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when validation fails.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(HostName))
            throw new ArgumentException("HostName is required for the RabbitMQ transport.", nameof(HostName));
        if (Port is < 1 or > 65535)
            throw new ArgumentException("Port must be between 1 and 65535.", nameof(Port));
        if (PublishChannelPoolSize < 1)
            throw new ArgumentException("PublishChannelPoolSize must be at least 1.", nameof(PublishChannelPoolSize));
    }
}
