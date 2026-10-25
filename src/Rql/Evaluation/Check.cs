using Rql.Nodes;

namespace Rql.Evaluation;

/// <summary>
/// Reports mistakes that would fail no matter what gets rolled, before anything is rolled.
/// </summary>
/// <remarks>
/// It does this by running the parts of the tree that nothing random can reach. Those parts give the same
/// answer every time, so any error they would raise, they raise now — and the rules being checked are the
/// ones the nodes already have, rather than a second set written out again here and left to drift.
/// </remarks>
internal static class Check
{
    public static void BeforeRolling(BoundQuery bound, Context context) =>
        Visit(bound.Root, Context.Settled(context.Tables), bound);

    private static void Visit(INode node, Context settled, BoundQuery bound)
    {
        if (IsSettled(node))
        {
            try
            {
                node.Evaluate(settled);
            }
            catch (EvalException e)
            {
                throw new QueryException(e.Message, bound.PositionOf(e.Blame));
            }
            return;
        }
        foreach (INode child in node.Children)
        {
            Visit(child, settled, bound);
        }
    }

    /// <summary>A node nothing random can reach, counting everything it is built from.</summary>
    private static bool IsSettled(INode node) => !node.Rolls && node.Children.All(IsSettled);
}
