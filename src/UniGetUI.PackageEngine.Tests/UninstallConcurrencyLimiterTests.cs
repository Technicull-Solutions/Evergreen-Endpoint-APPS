using UniGetUI.PackageOperations;

namespace UniGetUI.PackageEngine.Tests;

public class UninstallConcurrencyLimiterTests
{
    [Fact]
    public async Task FourthUninstallWaitsUntilASlotIsReleased()
    {
        var limiter = new UninstallConcurrencyLimiter(() => 3);
        using var first = await limiter.AcquireAsync(CancellationToken.None);
        using var second = await limiter.AcquireAsync(CancellationToken.None);
        using var third = await limiter.AcquireAsync(CancellationToken.None);
        Task<IDisposable> fourth = limiter.AcquireAsync(CancellationToken.None);
        Assert.False(fourth.IsCompleted);
        second.Dispose();
        using var admitted = await fourth.WaitAsync(TimeSpan.FromSeconds(5));
        // Double disposal must not create extra capacity.
        second.Dispose();
        using var cancel = new CancellationTokenSource();
        Task<IDisposable> fifth = limiter.AcquireAsync(cancel.Token);
        Assert.False(fifth.IsCompleted);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fifth);
    }

    [Fact]
    public async Task CancelingAWaiterDoesNotConsumeTheNextSlot()
    {
        var limiter = new UninstallConcurrencyLimiter(() => 1);
        using var first = await limiter.AcquireAsync(CancellationToken.None);
        using var cancel = new CancellationTokenSource();
        var waiting = limiter.AcquireAsync(cancel.Token);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        first.Dispose();
        using var next = await limiter.AcquireAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LoweringPolicyDoesNotCreateASecondIndependentPool()
    {
        int limit = 3;
        var limiter = new UninstallConcurrencyLimiter(() => limit);
        using var first = await limiter.AcquireAsync(CancellationToken.None);
        using var second = await limiter.AcquireAsync(CancellationToken.None);
        limit = 1;
        using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var waiting = limiter.AcquireAsync(cancel.Token);
        first.Dispose();
        Assert.False(waiting.IsCompleted);
        second.Dispose();
        using var next = await waiting;
    }
}
