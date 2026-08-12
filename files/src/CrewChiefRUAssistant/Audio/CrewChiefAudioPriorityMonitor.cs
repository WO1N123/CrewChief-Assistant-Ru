using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace CrewChiefRUAssistant.Audio;

public sealed class CrewChiefAudioPriorityMonitor : IDisposable
{
    private sealed class EndpointProbe : IDisposable
    {
        public EndpointProbe(
            MMDevice device,
            IReadOnlyList<AudioSessionControl> sessions)
        {
            Device = device;
            Sessions = sessions;
        }

        public MMDevice Device { get; }
        public IReadOnlyList<AudioSessionControl> Sessions { get; }

        public void Dispose()
        {
            foreach (var session in Sessions)
            {
                session.Dispose();
            }

            Device.Dispose();
        }
    }

    private readonly float _peakThreshold;
    private readonly TimeSpan _activityHold;
    private readonly int _scanIntervalMilliseconds;
    private readonly CancellationTokenSource _shutdown = new();

    private readonly List<EndpointProbe> _probes = [];

    private Task? _monitorTask;
    private DateTimeOffset _nextSessionRefresh = DateTimeOffset.MinValue;
    private long _lastActivityUtcTicks;
    private long _activeUntilUtcTicks;
    private long _lastErrorUtcTicks;
    private bool _disposed;

    public CrewChiefAudioPriorityMonitor(
        double peakThreshold,
        int activityHoldMilliseconds,
        int scanIntervalMilliseconds = 35)
    {
        _peakThreshold = (float)Math.Clamp(
            peakThreshold,
            0.0001,
            0.25);

        _activityHold = TimeSpan.FromMilliseconds(
            Math.Clamp(
                activityHoldMilliseconds,
                50,
                1500));

        _scanIntervalMilliseconds = Math.Clamp(
            scanIntervalMilliseconds,
            20,
            250);
    }

    public event EventHandler<string>? MonitorError;

    public bool IsSpeaking =>
        DateTimeOffset.UtcNow.UtcTicks <
        Interlocked.Read(ref _activeUntilUtcTicks);

    public DateTimeOffset? LastActivityAt
    {
        get
        {
            var ticks =
                Interlocked.Read(
                    ref _lastActivityUtcTicks);

            return ticks <= 0
                ? null
                : new DateTimeOffset(
                    ticks,
                    TimeSpan.Zero);
        }
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        if (_monitorTask is not null)
            return;

        _monitorTask = Task.Run(
            () => MonitorLoopAsync(
                _shutdown.Token));
    }

    public async Task<bool> WaitForQuietAsync(
        int quietPeriodMilliseconds,
        int maximumWaitMilliseconds,
        CancellationToken cancellationToken)
    {
        var quietPeriod = TimeSpan.FromMilliseconds(
            Math.Clamp(
                quietPeriodMilliseconds,
                0,
                5000));

        var maximumWait = TimeSpan.FromMilliseconds(
            Math.Clamp(
                maximumWaitMilliseconds,
                500,
                120000));

        var startedAt = DateTimeOffset.UtcNow;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var now = DateTimeOffset.UtcNow;
            var lastActivity = LastActivityAt;

            var quietLongEnough =
                !IsSpeaking &&
                (!lastActivity.HasValue ||
                 now - lastActivity.Value >= quietPeriod);

            if (quietLongEnough)
                return true;

            if (now - startedAt >= maximumWait)
                return false;

            await Task.Delay(
                35,
                cancellationToken);
        }
    }

    private async Task MonitorLoopAsync(
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (DateTimeOffset.UtcNow >=
                    _nextSessionRefresh)
                {
                    RefreshCrewChiefSessions();
                }

                if (ReadCrewChiefPeak())
                {
                    var now =
                        DateTimeOffset.UtcNow;

                    Interlocked.Exchange(
                        ref _lastActivityUtcTicks,
                        now.UtcTicks);

                    Interlocked.Exchange(
                        ref _activeUntilUtcTicks,
                        (now + _activityHold).UtcTicks);
                }
            }
            catch (Exception ex)
            {
                ClearProbes();

                _nextSessionRefresh =
                    DateTimeOffset.UtcNow
                        .AddSeconds(1);

                ReportErrorThrottled(ex.Message);
            }

            try
            {
                await Task.Delay(
                    _scanIntervalMilliseconds,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void RefreshCrewChiefSessions()
    {
        ClearProbes();

        using var enumerator =
            new MMDeviceEnumerator();

        var devices =
            enumerator.EnumerateAudioEndPoints(
                DataFlow.Render,
                DeviceState.Active);

        for (var deviceIndex = 0;
             deviceIndex < devices.Count;
             deviceIndex++)
        {
            var device = devices[deviceIndex];
            var matchingSessions =
                new List<AudioSessionControl>();

            try
            {
                device.AudioSessionManager
                    .RefreshSessions();

                var sessions =
                    device.AudioSessionManager
                        .Sessions;

                if (sessions is not null)
                {
                    for (var sessionIndex = 0;
                         sessionIndex < sessions.Count;
                         sessionIndex++)
                    {
                        AudioSessionControl? session = null;

                        try
                        {
                            session =
                                sessions[sessionIndex];

                            if (IsCrewChiefSession(
                                    session))
                            {
                                matchingSessions.Add(
                                    session);

                                session = null;
                            }
                        }
                        catch
                        {
                            // A session can disappear while it is enumerated.
                        }
                        finally
                        {
                            session?.Dispose();
                        }
                    }
                }

                if (matchingSessions.Count > 0)
                {
                    _probes.Add(
                        new EndpointProbe(
                            device,
                            matchingSessions));

                    device = null!;
                }
            }
            finally
            {
                device?.Dispose();
            }
        }

        _nextSessionRefresh =
            DateTimeOffset.UtcNow.AddSeconds(
                _probes.Count > 0
                    ? 4
                    : 1);
    }

    private bool ReadCrewChiefPeak()
    {
        var refreshNeeded = false;

        foreach (var probe in _probes)
        {
            foreach (var session in probe.Sessions)
            {
                try
                {
                    var meter =
                        session.AudioMeterInformation;

                    if (meter is not null &&
                        meter.MasterPeakValue >=
                        _peakThreshold)
                    {
                        return true;
                    }
                }
                catch
                {
                    refreshNeeded = true;
                }
            }
        }

        if (refreshNeeded)
        {
            _nextSessionRefresh =
                DateTimeOffset.MinValue;
        }

        return false;
    }

    private static bool IsCrewChiefSession(
        AudioSessionControl session)
    {
        uint processId;

        try
        {
            processId = session.GetProcessID;
        }
        catch
        {
            return false;
        }

        if (processId == 0 ||
            processId > int.MaxValue ||
            processId == Environment.ProcessId)
        {
            return false;
        }

        try
        {
            using var process =
                Process.GetProcessById(
                    (int)processId);

            // Do not use StartsWith("CrewChief"): our own process is named
            // CrewChiefRUAssistant and would then interrupt its own playback.
            return process.ProcessName.Equals(
                "CrewChiefV4",
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void ReportErrorThrottled(
        string message)
    {
        var now = DateTimeOffset.UtcNow;
        var previousTicks =
            Interlocked.Read(
                ref _lastErrorUtcTicks);

        if (previousTicks > 0 &&
            now -
            new DateTimeOffset(
                previousTicks,
                TimeSpan.Zero) <
            TimeSpan.FromSeconds(30))
        {
            return;
        }

        Interlocked.Exchange(
            ref _lastErrorUtcTicks,
            now.UtcTicks);

        MonitorError?.Invoke(
            this,
            message);
    }

    private void ClearProbes()
    {
        foreach (var probe in _probes)
        {
            probe.Dispose();
        }

        _probes.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _shutdown.Cancel();

        try
        {
            _monitorTask?.Wait(
                TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Cancellation during shutdown is harmless.
        }

        ClearProbes();
        _shutdown.Dispose();
    }
}
