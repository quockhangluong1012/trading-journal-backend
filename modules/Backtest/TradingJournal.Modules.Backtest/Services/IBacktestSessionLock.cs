using System.Collections.Concurrent;

namespace TradingJournal.Modules.Backtest.Services;

/// <summary>
/// Serializes balance-mutating operations for a single backtest session.
///
/// Several code paths do a read-modify-write on <c>BacktestSession.CurrentBalance</c>:
/// the playback auto-advance loop, manual position close, and finish-session. In a
/// modular monolith these all run in the same process, so a per-session in-memory
/// lock is enough to prevent the lost-update race (two paths reading the same balance
/// and both writing back, dropping one trade's PnL).
/// </summary>
public interface IBacktestSessionLock
{
    /// <summary>
    /// Acquires the lock for <paramref name="sessionId"/>. Dispose the returned handle
    /// (e.g. with <c>using</c>) to release it.
    /// </summary>
    Task<IAsyncDisposable> AcquireAsync(int sessionId, CancellationToken cancellationToken = default);
}

internal sealed class BacktestSessionLock : IBacktestSessionLock
{
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> Gates = new();

    public async Task<IAsyncDisposable> AcquireAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim gate = Gates.GetOrAdd(sessionId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Releaser(gate);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync()
        {
            // Guard against a double release if the handle is disposed more than once.
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                gate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
