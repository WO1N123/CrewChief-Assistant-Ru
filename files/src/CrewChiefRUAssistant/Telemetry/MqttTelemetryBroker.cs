using System.Net.Sockets;
using MQTTnet;
using MQTTnet.Server;

namespace CrewChiefRUAssistant.Telemetry;

public sealed class MqttTelemetryBroker : IAsyncDisposable
{
    private readonly int _port;
    private readonly TelemetryStore _store;
    private readonly bool _printIncomingTopics;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    private MqttServer? _server;
    private long _messageCount;
    private bool _disposed;

    public MqttTelemetryBroker(int port, TelemetryStore store, bool printIncomingTopics)
    {
        _port = port;
        _store = store;
        _printIncomingTopics = printIncomingTopics;
    }

    public bool IsRunning => _server?.IsStarted == true;
    public long MessageCount => Interlocked.Read(ref _messageCount);

    public async Task StartAsync()
    {
        await _lifecycleGate.WaitAsync();

        try
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MqttTelemetryBroker));

            if (IsRunning)
                return;

            await ReleaseServerAsync();

            var factory = new MqttFactory();
            var options = new MqttServerOptionsBuilder()
                .WithDefaultEndpoint()
                .WithDefaultEndpointBoundIPAddress(System.Net.IPAddress.Loopback)
                .WithDefaultEndpointPort(_port)
                .Build();

            var server = factory.CreateMqttServer(options);
            server.InterceptingPublishAsync += OnMessageAsync;
            _server = server;

            try
            {
                await server.StartAsync();
            }
            catch (Exception exception)
            {
                await ReleaseServerAsync();

                if (IsAddressAlreadyInUse(exception))
                {
                    throw new InvalidOperationException(
                        $"Порт MQTT {_port} уже занят. Возможно, CrewChief RU Assistant уже запущен в трее или этот порт использует другая MQTT-программа. " +
                        "Открой существующее окно из области уведомлений либо закрой второй процесс. Если порт занят другим брокером, выбери свободный порт в настройках и повторно примени настройку CrewChief.",
                        exception);
                }

                throw;
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lifecycleGate.WaitAsync();

        try
        {
            await ReleaseServerAsync();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task ReleaseServerAsync()
    {
        var server = _server;
        _server = null;

        if (server is null)
            return;

        server.InterceptingPublishAsync -= OnMessageAsync;

        try
        {
            if (server.IsStarted)
                await server.StopAsync();
        }
        finally
        {
            server.Dispose();
        }
    }

    private static bool IsAddressAlreadyInUse(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException socketException &&
                (socketException.SocketErrorCode == SocketError.AddressAlreadyInUse ||
                 socketException.ErrorCode == 10048))
            {
                return true;
            }
        }

        return false;
    }

    private Task OnMessageAsync(InterceptingPublishEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic ?? string.Empty;
        var payload = args.ApplicationMessage.ConvertPayloadToString();

        Interlocked.Increment(ref _messageCount);
        _store.Ingest(topic, payload);

        if (_printIncomingTopics)
            Console.WriteLine($"MQTT {topic}: {payload}");

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycleGate.WaitAsync();

        try
        {
            if (_disposed)
                return;

            _disposed = true;
            await ReleaseServerAsync();
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }
}
