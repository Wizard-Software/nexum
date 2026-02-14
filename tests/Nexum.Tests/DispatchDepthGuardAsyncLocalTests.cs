using Nexum.Internal;

namespace Nexum.Tests;

/// <summary>
/// Tests for <see cref="DispatchDepthGuard"/> AsyncLocal isolation.
/// Verifies that depth counters are independent per async flow, not per thread.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DispatchDepthGuardAsyncLocalTests
{
    [Fact]
    public async Task Enter_ParallelAsyncFlows_HaveIndependentDepthCountersAsync()
    {
        // Arrange — two parallel async flows, each entering depth 2
        var flow1ReachedDepth2 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var flow2ReachedDepth2 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ct = TestContext.Current.CancellationToken;

        // Act & Assert
        var task1 = Task.Run(async () =>
        {
            using var scope1 = DispatchDepthGuard.Enter(2); // depth = 1
            using var scope2 = DispatchDepthGuard.Enter(2); // depth = 2

            flow1ReachedDepth2.SetResult();

            // Wait for flow2 to also reach depth 2
            await flow2ReachedDepth2.Task;

            // If counters were shared, depth would be 4 and this would have thrown
        }, ct);

        var task2 = Task.Run(async () =>
        {
            using var scope1 = DispatchDepthGuard.Enter(2); // depth = 1
            using var scope2 = DispatchDepthGuard.Enter(2); // depth = 2

            flow2ReachedDepth2.SetResult();

            // Wait for flow1 to also reach depth 2
            await flow1ReachedDepth2.Task;

            // If counters were shared, depth would be 4 and this would have thrown
        }, ct);

        // Both flows should complete without exception
        await Task.WhenAll(task1, task2);
    }

    [Fact]
    public async Task Enter_ConcurrentDispatches_DoNotInterfereAsync()
    {
        // Arrange — many concurrent flows each pushing depth to the limit
        const int MaxDepth = 3;
        const int ConcurrencyLevel = 20;
        var ct = TestContext.Current.CancellationToken;

        // Act
        var tasks = Enumerable.Range(0, ConcurrencyLevel).Select(_ => Task.Run(() =>
        {
            // Each flow enters up to max depth — should succeed independently
            using var s1 = DispatchDepthGuard.Enter(MaxDepth);
            using var s2 = DispatchDepthGuard.Enter(MaxDepth);
            using var s3 = DispatchDepthGuard.Enter(MaxDepth);
        }, ct));

        // Assert — all flows succeed (no cross-flow interference)
        var act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
    }
}
