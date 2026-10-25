using Rql.Nodes;

namespace Rql.Functions;

// Roll20's math functions, written lower case as Roll20 writes them; function names ignore case anyway.

/// <summary>Rounds a number down, so <c>floor(7/2)</c> is 3 and <c>floor(-7/2)</c> is -4.</summary>
/// <param name="Value">The number to round.</param>
[Function("floor", Description = "Rounds down: floor(7/2) is 3.")]
public sealed record Floor(Node<decimal> Value) : Function<decimal>
{
    /// <inheritdoc/>
    public override decimal Evaluate(Context context) => decimal.Floor(Value.Evaluate(context));
}

/// <summary>Rounds a number up, so <c>ceil(7/2)</c> is 4 and <c>ceil(-7/2)</c> is -3.</summary>
/// <param name="Value">The number to round.</param>
[Function("ceil", Description = "Rounds up: ceil(7/2) is 4.")]
public sealed record Ceil(Node<decimal> Value) : Function<decimal>
{
    /// <inheritdoc/>
    public override decimal Evaluate(Context context) => decimal.Ceiling(Value.Evaluate(context));
}

/// <summary>
/// Rounds a number to the nearest whole number, halves going up as Roll20 does: <c>round(2.5)</c> is 3 and
/// <c>round(-2.5)</c> is -2.
/// </summary>
/// <param name="Value">The number to round.</param>
[Function("round", Description = "Rounds to the nearest whole number, halves going up: round(2.5) is 3.")]
public sealed record Round(Node<decimal> Value) : Function<decimal>
{
    /// <inheritdoc/>
    public override decimal Evaluate(Context context) => decimal.Floor(Value.Evaluate(context) + 0.5m);
}

/// <summary>Makes a number positive, so <c>abs(-3)</c> is 3.</summary>
/// <param name="Value">The number to make positive.</param>
[Function("abs", Description = "Makes a number positive: abs(-3) is 3.")]
public sealed record Abs(Node<decimal> Value) : Function<decimal>
{
    /// <inheritdoc/>
    public override decimal Evaluate(Context context) => Math.Abs(Value.Evaluate(context));
}
