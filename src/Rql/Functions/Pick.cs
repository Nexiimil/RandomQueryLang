using Rql.Nodes;
using Rql.Tables;

namespace Rql.Functions;

/// <summary>Picks a row by counting down from the top of a table, so row 1 is the first row.</summary>
[Function("Pick", Description = "Picks a row by counting down from the top of the table, starting at 1.")]
public sealed record Pick(Node<Table> Table, Node<decimal> Row) : Function<Table>
{
    /// <inheritdoc/>
    public override Table Evaluate(Context context)
    {
        Table source = Table.Evaluate(context);
        int wanted = Numbers.Whole(Row.Evaluate(context), Row, "rows");
        if (wanted < 1 || wanted > source.Count)
        {
            string rows = source.Count == 0 ? "has no rows" : $"has rows 1 to {source.Count}";
            throw new EvalException(Row, $"can't pick row {wanted}: {source.Name} {rows}");
        }
        return source.WithThings([source.Things[wanted - 1]]);
    }
}
