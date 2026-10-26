using UniGetUI.Core.SettingsEngine;

namespace UniGetUI.PackageOperations;

/// <summary>One shared limit for every uninstall execution path in this client process.</summary>
internal sealed class UninstallConcurrencyLimiter(Func<int> maximum)
{
    internal static readonly UninstallConcurrencyLimiter Shared = new(
        () => ManagedPackageManagerConfiguration.MaximumConcurrentUninstalls);

    private readonly Lock _sync = new();
    private int _active;
    private TaskCompletionSource _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Task changed;
            lock (_sync)
            {
                if (_active < maximum())
                {
                    _active++;
                    return new Lease(this);
                }
                changed = _changed.Task;
            }
            await changed.WaitAsync(cancellationToken);
        }
    }

    private void Release()
    {
        lock (_sync)
        {
            _active--;
            var previous = _changed;
            _changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            previous.TrySetResult();
        }
    }

    private sealed class Lease(UninstallConcurrencyLimiter owner) : IDisposable
    {
        private UninstallConcurrencyLimiter? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release();
    }
}
