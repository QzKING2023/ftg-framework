#nullable enable
using FTG_Framework.Core;
using FTG_Framework.Input;
using Xunit;

namespace FTG_Framework.Tests;

public class DefaultSOCDResolverTests
{
    private readonly DefaultSOCDResolver _resolver = new();

    [Fact]
    public void SingleDirection_Left_ReturnsBack()
    {
        var result = _resolver.Resolve(left: true, right: false, down: false, up: false);
        Assert.Equal(DirectionValue.Back, result);
    }

    [Fact]
    public void SingleDirection_Right_ReturnsForward()
    {
        var result = _resolver.Resolve(left: false, right: true, down: false, up: false);
        Assert.Equal(DirectionValue.Forward, result);
    }

    [Fact]
    public void SingleDirection_Down_ReturnsDown()
    {
        var result = _resolver.Resolve(left: false, right: false, down: true, up: false);
        Assert.Equal(DirectionValue.Down, result);
    }

    [Fact]
    public void SingleDirection_Up_ReturnsUp()
    {
        var result = _resolver.Resolve(left: false, right: false, down: false, up: true);
        Assert.Equal(DirectionValue.Up, result);
    }

    [Fact]
    public void Diagonal_DownForward_ReturnsDownForward()
    {
        var result = _resolver.Resolve(left: false, right: true, down: true, up: false);
        Assert.Equal(DirectionValue.DownForward, result);
    }

    [Fact]
    public void Diagonal_DownBack_ReturnsDownBack()
    {
        var result = _resolver.Resolve(left: true, right: false, down: true, up: false);
        Assert.Equal(DirectionValue.DownBack, result);
    }

    [Fact]
    public void Diagonal_UpForward_ReturnsUpForward()
    {
        var result = _resolver.Resolve(left: false, right: true, down: false, up: true);
        Assert.Equal(DirectionValue.UpForward, result);
    }

    [Fact]
    public void Diagonal_UpBack_ReturnsUpBack()
    {
        var result = _resolver.Resolve(left: true, right: false, down: false, up: true);
        Assert.Equal(DirectionValue.UpBack, result);
    }

    [Fact]
    public void NoKeys_ReturnsNeutral()
    {
        var result = _resolver.Resolve(left: false, right: false, down: false, up: false);
        Assert.Equal(DirectionValue.Neutral, result);
    }

    [Fact]
    public void Socd_LeftRight_ReturnsNeutral()
    {
        var result = _resolver.Resolve(left: true, right: true, down: false, up: false);
        Assert.Equal(DirectionValue.Neutral, result);
    }

    [Fact]
    public void Socd_UpDown_ReturnsUp()
    {
        var result = _resolver.Resolve(left: false, right: false, down: true, up: true);
        Assert.Equal(DirectionValue.Up, result);
    }

    [Fact]
    public void Socd_AllFour_ReturnsUp()
    {
        // Resolution order: (1) U+D → Up, (2) L+R → Neutral (horizontal only)
        // Vertical Up survives; horizontal cleared. Final: Up.
        var result = _resolver.Resolve(left: true, right: true, down: true, up: true);
        Assert.Equal(DirectionValue.Up, result);
    }

    [Fact]
    public void Socd_LeftRight_WithDown_ReturnsDown()
    {
        // L+R → Neutral (horizontal), Down passes through → Down
        var result = _resolver.Resolve(left: true, right: true, down: true, up: false);
        Assert.Equal(DirectionValue.Down, result);
    }

    [Fact]
    public void Socd_LeftRight_WithUp_ReturnsUp()
    {
        // L+R → Neutral (horizontal), Up passes through → Up
        var result = _resolver.Resolve(left: true, right: true, down: false, up: true);
        Assert.Equal(DirectionValue.Up, result);
    }

    [Fact]
    public void Socd_UpDown_WithLeft_ReturnsUpBack()
    {
        // U+D → Up, Left passes through → UpBack
        var result = _resolver.Resolve(left: true, right: false, down: true, up: true);
        Assert.Equal(DirectionValue.UpBack, result);
    }

    [Fact]
    public void Socd_UpDown_WithRight_ReturnsUpForward()
    {
        // U+D → Up, Right passes through → UpForward
        var result = _resolver.Resolve(left: false, right: true, down: true, up: true);
        Assert.Equal(DirectionValue.UpForward, result);
    }

    [Fact]
    public void CustomResolver_ReplacesDefaultBehavior()
    {
        // Custom resolver: L+R→Left (not Neutral), U+D→Neutral (not Up)
        var customResolver = new CustomSOCDResolver();
        var result = customResolver.Resolve(left: true, right: true, down: false, up: false);
        Assert.Equal(DirectionValue.Back, result); // L+R → Left=Back

        var result2 = customResolver.Resolve(left: false, right: false, down: true, up: true);
        Assert.Equal(DirectionValue.Neutral, result2); // U+D → Neutral
    }

    private sealed class CustomSOCDResolver : ISOCDResolver
    {
        public DirectionValue Resolve(bool left, bool right, bool down, bool up)
        {
            // Custom rules: L+R→Left, U+D→Neutral
            bool l = left, r = right, d = down, u = up;
            if (l && r) { r = false; } // L+R → Left
            if (d && u) { d = false; u = false; } // U+D → Neutral
            return GameLoop.ComputeDirection(l, r, d, u);
        }
    }

    [Fact]
    public void InvalidEnumValue_FallsBackToNeutral()
    {
        var badResolver = new InvalidEnumResolver();
        var result = GameLoop.SafeResolve(badResolver, left: false, right: false, down: false, up: false);
        Assert.Equal(DirectionValue.Neutral, result);
    }

    private sealed class InvalidEnumResolver : ISOCDResolver
    {
        public DirectionValue Resolve(bool left, bool right, bool down, bool up)
            => (DirectionValue)999;
    }
}
