using Rql.Nodes;

namespace Rql.Evaluation;

/// <summary>
/// A tree, with where in the query each of its nodes was written.
/// </summary>
/// <remarks>
/// Positions are kept beside the tree rather than on the nodes, so that a node's properties stay only its
/// arguments and a tree built by hand doesn't have to invent them. Nodes are looked up by reference, so
/// two nodes that happen to be equal still have their own positions.
/// </remarks>
internal sealed class BoundQuery
{
    private readonly Dictionary<INode, int> positions;

    internal BoundQuery(INode root, Dictionary<INode, int> positions)
    {
        Root = root;
        this.positions = new Dictionary<INode, int>(positions, ReferenceEqualityComparer.Instance);
    }

    public INode Root { get; }

    public int PositionOf(INode node) => positions.GetValueOrDefault(node, QueryException.Nowhere);
}
