using Rql.Nodes;

namespace Rql.Functions;

// Roll20's math functions, written lower case as Roll20 writes them; function names ignore case anyway.

[Function("floor", Description = "Rounds down: floor(7/2) is 3.")]
public sealed record Floor(Node<decimal> Value) : Function<decimal>
{
    public override decimal Evaluate(Context context) => decimal.Floor(Value.Evaluate(context));
}

[Function("ceil", Description = "Rounds up: ceil(7/2) is 4.")]
public sealed record Ceil(Node<decimal> Value) : Function<decimal>
{
    public override decimal Evaluate(Context context) => decimal.Ceiling(Value.Evaluate(context));
}

[Function("round", Description = "Rounds to the nearest whole number, halves going up: round(2.5) is 3.")]
public sealed record Round(Node<decimal> Value) : Function<decimal>
{
    public override decimal Evaluate(Context context) => decimal.Floor(Value.Evaluate(context) + 0.5m);
}

[Function("abs", Description = "Makes a number positive: abs(-3) is 3.")]
public sealed record Abs(Node<decimal> Value) : Function<decimal>
{
    public override decimal Evaluate(Context context) => Math.Abs(Value.Evaluate(context));
}
