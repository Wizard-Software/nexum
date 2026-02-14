using Nexum.Abstractions;
using Nexum.Internal;

namespace Nexum.Tests;

[Trait("Category", "Unit")]
public sealed class DispatchDepthGuardTests
{
    [Fact]
    public void Enter_WithinLimit_Succeeds()
    {
        // Arrange & Act & Assert
        var act = () =>
        {
            using var _ = DispatchDepthGuard.Enter(5);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Enter_ExceedingLimit_ThrowsNexumDispatchDepthExceededException()
    {
        // Arrange & Act
        var act = () =>
        {
            using var scope1 = DispatchDepthGuard.Enter(1);
            using var scope2 = DispatchDepthGuard.Enter(1); // Exceeds limit
        };

        // Assert
        act.Should()
            .Throw<NexumDispatchDepthExceededException>()
            .WithMessage("*1*");
    }

    [Fact]
    public void Dispose_DecrementsDepth_AllowsReentry()
    {
        // Act — first entry then dispose
        using (var scope1 = DispatchDepthGuard.Enter(1))
        {
            // First entry succeeds
        } // Dispose decrements depth

        // Assert — second entry should succeed after disposal
        var act = () =>
        {
            using var scope2 = DispatchDepthGuard.Enter(1);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Enter_NestedCalls_TracksDepthCorrectly()
    {
        // Act & Assert — should succeed for depths 1, 2, 3
        var act = () =>
        {
            using var scope1 = DispatchDepthGuard.Enter(3); // depth = 1
            using var scope2 = DispatchDepthGuard.Enter(3); // depth = 2
            using var scope3 = DispatchDepthGuard.Enter(3); // depth = 3
        };

        act.Should().NotThrow();

        // Act & Assert — depth 4 should fail
        var actExceeding = () =>
        {
            using var scope1 = DispatchDepthGuard.Enter(3); // depth = 1
            using var scope2 = DispatchDepthGuard.Enter(3); // depth = 2
            using var scope3 = DispatchDepthGuard.Enter(3); // depth = 3
            using var scope4 = DispatchDepthGuard.Enter(3); // depth = 4 -> exceeds
        };

        actExceeding.Should()
            .Throw<NexumDispatchDepthExceededException>()
            .WithMessage("*3*");
    }
}
