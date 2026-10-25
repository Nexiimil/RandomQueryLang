using Rql.Nodes;
using Rql.Tables;

namespace Rql.Functions;

/// <summary>Rolls on a table <c>Count</c> times. Every roll is separate, so the same row can come up more than once.</summary>
[Function("Roll", Description = "Rolls on a table count times, so the same row can come up more than once.")]
public sealed record RollTable(Node<Table> Table, [Default(1)] Node<decimal> Count) : Function<Table>
{
    public override bool Rolls => true;

    public override Table Evaluate(Context context)
    {
        int times = Numbers.Whole(Count.Evaluate(context), Count, "times to roll");
        if (times < 0)
        {
            throw new EvalException(Count, $"can't roll a negative number of times ({times})");
        }

        Table source = Table.Evaluate(context);
        if (times == 0)
        {
            return source.WithThings([]);
        }

        // Chances are treated as weights, so a table with rows dropped from it still rolls fairly.
        double totalChance = source.Things.Sum(thing => thing.Chance);
        if (totalChance <= 0)
        {
            throw new EvalException(Table, $"{source.Name} has no rows that can be rolled");
        }

        var rolled = new List<Thing>(times);
        for (int i = 0; i < times; i++)
        {
            rolled.Add(RollOnce(source, totalChance, context.Random));
        }
        return source.WithThings(rolled);
    }

    private static Thing RollOnce(Table table, double totalChance, Random random)
    {
        double target = random.NextDouble() * totalChance;
        double cumulative = 0;
        Thing? lastRollable = null;
        foreach (Thing thing in table.Things.Where(thing => thing.Chance > 0))
        {
            cumulative += thing.Chance;
            lastRollable = thing;
            if (target < cumulative)
            {
                return thing;
            }
        }
        // Rounding can leave the sum of chances just under the target.
        return lastRollable!;
    }
}
