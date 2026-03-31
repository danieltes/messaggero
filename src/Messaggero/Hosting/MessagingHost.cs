using Messaggero.Abstractions;
using Messaggero.Configuration;
using Messaggero.Errors;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Messaggero.Hosting;

/// <summary>
/// Runtime that manages adapter and handler lifecycles.
/// Implements <see cref="IHostedService"/> for ASP.NET Core / Generic Host integration.
/// </summary>
public sealed class MessagingHost : IHostedService, IAsyncDisposable
{
    private readonly MessagingConfiguration _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessagingHost> _logger;
    private readonly Dictionary<string, ITransportAdapter> _adapters = new(StringComparer.OrdinalIgnoreCase);
    private HandlerDispatcher? _dispatcher;

    public MessagingHost(
        MessagingConfiguration config,
        IServiceProvider serviceProvider,
        ILogger<MessagingHost> logger)
    {
        _config = config;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>Active adapter instances, keyed by name.</summary>
    public IReadOnlyDictionary<string, ITransportAdapter> Adapters => _adapters;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // Create adapter instances
        foreach (var (name, registration) in _config.Transports)
        {
            var adapter = registration.AdapterFactory(_serviceProvider);
            _adapters[name] = adapter;
        }

        // Start all adapters in parallel with per-adapter isolation
        var startTasks = _adapters.Select(async kvp =>
        {
            try
            {
                await kvp.Value.StartAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Transport {TransportName} started", kvp.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transport {TransportName} failed to start", kvp.Key);
                throw;
            }
        });

        await Task.WhenAll(startTasks).ConfigureAwait(false);

        // Initialize handler lifecycles
        foreach (var handler in _config.Handlers)
        {
            var instance = _serviceProvider.GetService(handler.HandlerType);
            if (instance is IHandlerLifecycle lifecycle)
            {
                await lifecycle.InitializeAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        // Set up handler dispatch (subscriptions) if there are handlers
        if (_config.Handlers.Count > 0)
        {
            _dispatcher = new HandlerDispatcher(_config, _adapters, _serviceProvider, _logger);
            await _dispatcher.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop dispatcher first
        if (_dispatcher is not null)
        {
            await _dispatcher.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        // Dispose handler lifecycles
        foreach (var handler in _config.Handlers)
        {
            var instance = _serviceProvider.GetService(handler.HandlerType);
            if (instance is IHandlerLifecycle lifecycle)
            {
                try
                {
                    await lifecycle.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Handler {HandlerType} lifecycle dispose failed", handler.HandlerType.Name);
                }
            }
        }

        // Stop all adapters in parallel with per-adapter isolation
        var stopTasks = _adapters.Select(async kvp =>
        {
            try
            {
                await kvp.Value.StopAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Transport {TransportName} stopped", kvp.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transport {TransportName} failed to stop gracefully", kvp.Key);
            }
        });

        await Task.WhenAll(stopTasks).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var adapter in _adapters.Values)
        {
            await adapter.DisposeAsync().ConfigureAwait(false);
        }
        _adapters.Clear();
    }
}
