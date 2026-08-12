using System.Diagnostics;
using Avalonia.Threading;

namespace ShapeEngine.Avalonia;

/// <summary>
/// An <see cref="IDispatcherImpl"/> that runs Avalonia's dispatcher work on the game loop thread.
/// </summary>
/// <remarks>
/// ShapeEngine has no message loop to post to, so signals and timers are queued and drained once per
/// frame from <see cref="Pump"/>. Everything Avalonia schedules on <c>Dispatcher.UIThread</c> therefore
/// runs on the thread that owns the OpenGL context.
/// </remarks>
internal sealed class ShapeEngineDispatcherImpl : IDispatcherImpl
{
    /// <summary>
    /// Handlers commonly signal again; draining those in the same frame avoids a frame of latency per
    /// continuation, and the cap stops a self-perpetuating signal from stalling the game.
    /// </summary>
    private const int MaxSignalsPerPump = 16;

    private const long NoTimer = Int64.MinValue;

    private readonly Thread mainThread;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Lock gate = new();

    private int signalPending;
    private long timerDueTime = NoTimer;

    public ShapeEngineDispatcherImpl(Thread mainThread) => this.mainThread = mainThread;

    public long Now => clock.ElapsedMilliseconds;

    public bool CurrentThreadIsLoopThread => mainThread == Thread.CurrentThread;

    public event Action? Signaled;

    public event Action? Timer;

    public void Signal() => Interlocked.Exchange(ref signalPending, 1);

    public void UpdateTimer(long? dueTimeInMs)
    {
        lock (gate) timerDueTime = dueTimeInMs ?? NoTimer;
    }

    /// <summary>Runs any dispatcher work that came due. Call once per frame on the game loop thread.</summary>
    public void Pump()
    {
        var timerDue = false;
        lock (gate)
        {
            if (timerDueTime != NoTimer && Now >= timerDueTime)
            {
                timerDueTime = NoTimer;
                timerDue = true;
            }
        }

        if (timerDue) Timer?.Invoke();

        for (var i = 0; i < MaxSignalsPerPump && Interlocked.Exchange(ref signalPending, 0) == 1; i++)
        {
            Signaled?.Invoke();
        }
    }
}
