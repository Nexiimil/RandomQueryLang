using Rql.Nodes;
using Rql.Tables;

namespace Rql.Functions;

/// <summary>
/// Rolls dice <c>Count</c> times, giving a row for every die kept. <c>Roll(4d6, 2)</c> gives 8 rows, which is
/// why dice and tables are rolled by separate nodes rather than one that checks what it was given.
/// </summary>
[Function("Roll", Description = "Rolls dice count times, giving a row for every die: Roll(4d6, 2) gives 8 rows.")]
public sealed record RollDice(Node<Dice> Dice, [Default(1)] Node<decimal> Count) : Function<Table>
{
    /// <inheritdoc/>
    public override bool Rolls => true;

    /// <inheritdoc/>
    public override Table Evaluate(Context context)
    {
        int times = Numbers.Whole(Count.Evaluate(context), Count, "times to roll");
        if (times < 0)
        {
            throw new EvalException(Count, $"can't roll a negative number of times ({times})");
        }

        Dice rolling = Dice.Evaluate(context);
        var faces = new List<Thing>();
        for (int i = 0; i < times; i++)
        {
            faces.AddRange(context.Roll(rolling, Dice).AsTable().Things);
        }
        return new Table(rolling.Display(), faces);
    }
}
