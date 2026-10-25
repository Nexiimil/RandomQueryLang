namespace Rql.Nodes;

/// <summary>
/// Arithmetic on numbers, which are decimals as in Roll20: <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c>, <c>%</c>
/// (the remainder, taking the sign of the number divided) and <c>**</c> (raising to a power).
/// </summary>
public sealed record Arithmetic(string Operator, Node<decimal> Left, Node<decimal> Right) : Node<decimal>
{
    /// <inheritdoc/>
    public override IReadOnlyList<INode> Children => [Left, Right];

    /// <inheritdoc/>
    public override decimal Evaluate(Context context)
    {
        decimal left = Left.Evaluate(context);
        decimal right = Right.Evaluate(context);
        try
        {
            return Operator switch
            {
                "+" => left + right,
                "-" => left - right,
                "*" => left * right,
                "/" => right == 0 ? throw new EvalException(Right, "can't divide by 0") : left / right,
                "%" => right == 0 ? throw new EvalException(Right, "can't divide by 0") : left % right,
                "**" => Power(left, right),
                _ => throw new InvalidOperationException($"{Operator} isn't arithmetic"),
            };
        }
        catch (OverflowException)
        {
            throw new EvalException(this, $"{Numbers.Display(left)} {Operator} {Numbers.Display(right)} is too large");
        }
    }

    private decimal Power(decimal number, decimal power)
    {
        // Whole powers are worked out exactly; anything else, like a square root, has to go through doubles.
        if (power == decimal.Truncate(power) && Math.Abs(power) <= 1000)
        {
            decimal result = 1;
            for (int i = 0; i < Math.Abs((int)power); i++)
            {
                result *= number;
            }
            return power >= 0 ? result
                : result == 0 ? throw new EvalException(Right, "can't raise 0 to a negative power")
                : 1 / result;
        }
        double raised = Math.Pow((double)number, (double)power);
        return double.IsFinite(raised)
            ? (decimal)raised
            : throw new EvalException(this, $"{Numbers.Display(number)} ** {Numbers.Display(power)} isn't a number");
    }
}

/// <summary>A number made negative, like <c>-d6</c>.</summary>
public sealed record Negation(Node<decimal> Operand) : Node<decimal>
{
    /// <inheritdoc/>
    public override IReadOnlyList<INode> Children => [Operand];

    /// <inheritdoc/>
    public override decimal Evaluate(Context context) => -Operand.Evaluate(context);
}
