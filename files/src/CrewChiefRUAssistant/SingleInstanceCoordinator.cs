using System.Threading;

namespace CrewChiefRUAssistant;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\CrewChiefRUAssistant.SingleInstance";
    private const string ActivationEventName = @"Local\CrewChiefRUAssistant.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _activationEvent;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _listener;
    private bool _ownsMutex;

    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(
            initiallyOwned: true,
            name: MutexName,
            createdNew: out var createdNew);

        IsPrimaryInstance = createdNew;
        _ownsMutex = createdNew;

        if (createdNew)
        {
            _activationEvent = new EventWaitHandle(
                initialState: false,
                mode: EventResetMode.AutoReset,
                name: ActivationEventName);
        }
        else
        {
            ActivationSignalSent = TrySignalPrimaryInstance();
        }
    }

    public bool IsPrimaryInstance { get; }
    public bool ActivationSignalSent { get; }

    public void StartListening(Action activate)
    {
        if (!IsPrimaryInstance || _activationEvent is null || _listener is not null)
            return;

        _listener = Task.Run(() =>
        {
            var handles = new WaitHandle[]
            {
                _activationEvent,
                _shutdown.Token.WaitHandle
            };

            while (true)
            {
                var signaled = WaitHandle.WaitAny(handles);
                if (signaled == 1)
                    return;

                try
                {
                    activate();
                }
                catch
                {
                    // The main window may be closing while a second instance starts.
                }
            }
        });
    }

    private static bool TrySignalPrimaryInstance()
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            try
            {
                using var activation =
                    EventWaitHandle.OpenExisting(ActivationEventName);
                return activation.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(75);
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    public void Dispose()
    {
        _shutdown.Cancel();

        try
        {
            _activationEvent?.Set();
            _listener?.Wait(500);
        }
        catch
        {
            // Process shutdown must not be blocked by the activation listener.
        }

        _activationEvent?.Dispose();
        _shutdown.Dispose();

        if (_ownsMutex)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The operating system will release the mutex on process exit.
            }

            _ownsMutex = false;
        }

        _mutex.Dispose();
    }
}
