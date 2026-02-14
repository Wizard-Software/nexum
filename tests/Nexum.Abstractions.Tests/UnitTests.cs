namespace Nexum.Abstractions.Tests;

[Trait("Category", "Unit")]
public class UnitTests
{
    [Fact]
    public void Equals_TwoUnits_ReturnsTrue()
    {
        Unit.Value.Equals(default).Should().BeTrue();
    }

    [Fact]
    public void Equals_BoxedUnit_ReturnsTrue()
    {
        Unit.Value.Equals((object)Unit.Value).Should().BeTrue();
    }

    [Fact]
    public void Equals_NonUnitObject_ReturnsFalse()
    {
        Unit.Value.Equals("not a unit").Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        Unit.Value.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void CompareTo_TwoUnits_ReturnsZero()
    {
        Unit.Value.CompareTo(default).Should().Be(0);
    }

    [Fact]
    public void ToString_ReturnsParentheses()
    {
        Unit.Value.ToString().Should().Be("()");
    }

    [Fact]
    public void GetHashCode_AlwaysReturnsZero()
    {
        Unit.Value.GetHashCode().Should().Be(0);
    }

    [Fact]
    public void EqualityOperator_TwoUnits_ReturnsTrue()
    {
        (Unit.Value == default).Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_TwoUnits_ReturnsFalse()
    {
        (Unit.Value != default).Should().BeFalse();
    }

    [Fact]
    public void Value_IsDefault()
    {
        Unit.Value.Should().Be(default);
    }

    [Fact]
    public void Unit_ImplementsIEquatable()
    {
        typeof(Unit).GetInterfaces().Should().Contain(typeof(IEquatable<Unit>));
    }

    [Fact]
    public void Unit_ImplementsIComparable()
    {
        typeof(Unit).GetInterfaces().Should().Contain(typeof(IComparable<Unit>));
    }

    [Fact]
    public void Unit_IsReadonlyStruct()
    {
        typeof(Unit).IsValueType.Should().BeTrue();
        typeof(Unit).GetCustomAttributesData()
            .Should().Contain(a => a.AttributeType.Name == "IsReadOnlyAttribute");
    }

    [Fact]
    public void Default_IsValidUnit()
    {
        default(Unit).Equals(Unit.Value).Should().BeTrue();
    }
}
