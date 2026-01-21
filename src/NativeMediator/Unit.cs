namespace NativeMediator;

/// <summary>
/// Represents a void type, since void is not a valid return type in C#.
/// Used for requests that do not return a value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
    /// <summary>
    /// Default and only value of the <see cref="Unit"/> type.
    /// </summary>
    public static readonly Unit Value = new();

    /// <summary>
    /// Task returning the default unit value.
    /// </summary>
    public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);

    /// <summary>
    /// ValueTask returning the default unit value.
    /// </summary>
    public static readonly ValueTask<Unit> ValueTask = new(Value);

    /// <inheritdoc/>
    public int CompareTo(Unit other) => 0;

    /// <inheritdoc/>
    int IComparable.CompareTo(object? obj) => 0;

    /// <inheritdoc/>
    public override int GetHashCode() => 0;

    /// <inheritdoc/>
    public bool Equals(Unit other) => true;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc/>
    public override string ToString() => "()";

    /// <summary>
    /// Determines whether two <see cref="Unit"/> instances are equal.
    /// </summary>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Determines whether two <see cref="Unit"/> instances are not equal.
    /// </summary>
    public static bool operator !=(Unit left, Unit right) => false;
}
