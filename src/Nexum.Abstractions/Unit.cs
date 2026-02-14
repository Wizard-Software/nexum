namespace Nexum.Abstractions;

/// <summary>
/// Represents a void return type for commands that do not produce a result.
/// Used as TResult in <see cref="ICommand{TResult}"/> for void commands
/// and <see cref="IVoidCommand"/>.
/// </summary>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>
{
    /// <summary>Gets the single value of <see cref="Unit"/>.</summary>
    public static readonly Unit Value = default;

    /// <inheritdoc />
    public bool Equals(Unit other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <inheritdoc cref="IComparable{T}.CompareTo"/>
    public int CompareTo(Unit other) => 0;

    /// <inheritdoc />
    public override string ToString() => "()";

#pragma warning disable IDE0060 // Parameters are required by operator signature but unused by design — all Units are equal
    /// <summary>Returns <see langword="true"/>; all <see cref="Unit"/> values are equal.</summary>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>Returns <see langword="false"/>; all <see cref="Unit"/> values are equal.</summary>
    public static bool operator !=(Unit left, Unit right) => false;
#pragma warning restore IDE0060
}
