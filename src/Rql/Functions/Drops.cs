using Rql.Nodes;
using Rql.Tables;

namespace Rql.Functions;

/// <summary>What <see cref="DropLowest"/> and <see cref="DropHighest"/> share.</summary>
internal static class Drops
{
    public static Table Drop(Node<Table> table, Node<decimal> count, bool lowest, Context context)
    {
        int dropping = Numbers.Whole(count.Evaluate(context), count, "rows to drop");
        if (dropping < 0)
        {
            throw new EvalException(count, $"can't drop a negative number of rows ({dropping})");
        }

        Table source = table.Evaluate(context);
        IReadOnlyList<Thing> things = source.Things;
        int[] order = [.. Enumerable.Range(0, things.Count)];
        // Shuffling before a stable sort means rows with the same value are dropped at random.
        context.Random.Shuffle(order);
        IEnumerable<int> sorted = lowest
            ? order.OrderBy(index => things[index].Value)
            : order.OrderByDescending(index => things[index].Value);

        var dropped = sorted.Take(dropping).ToHashSet();
        return source.WithThings(things.Where((_, index) => !dropped.Contains(index)));
    }
}
