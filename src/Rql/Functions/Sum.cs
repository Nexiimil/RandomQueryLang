using Rql.Nodes;
using Rql.Tables;

namespace Rql.Functions;

/// <summary>Adds up the values of every row in a table.</summary>
[Function("Sum", Description = "Adds up the values of every row in the table.")]
public sealed record Sum(Node<Table> Table) : Function<decimal>
{
    /// <inheritdoc/>
    public override decimal Evaluate(Context context) => Table.Evaluate(context).Total();
}
