using Rql.Nodes;
using Rql.Tables;

namespace Rql.Functions;

/// <summary>
/// Removes the <c>Count</c> lowest valued rows from a table. Where more rows tie for lowest than are being
/// dropped, which of them goes is decided at random.
/// </summary>
[Function("DropLowest", Description = "Removes the count lowest valued rows; ties are broken at random.")]
public sealed record DropLowest(Node<Table> Table, [Default(1)] Node<decimal> Count) : Function<Table>
{
    /// <summary>Ties between equal rows are broken at random, so even a settled table can't be dropped early.</summary>
    public override bool Rolls => true;

    public override Table Evaluate(Context context) => Drops.Drop(Table, Count, lowest: true, context);
}
