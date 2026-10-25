namespace Rql.Nodes;

/// <summary>
/// A node seen without its type, which is how a walk over a tree sees every node: what a tree is built
/// from has different types from node to node.
/// </summary>
public interface INode
{
    /// <summary>The type of the value <see cref="Evaluate"/> produces.</summary>
    Type ValueType { get; }

    /// <summary>
    /// The nodes this one is built from, in the order they were written. Enough for any walk over the
    /// tree, which is why nodes don't need to be a closed set.
    /// </summary>
    IReadOnlyList<INode> Children { get; }

    /// <summary>
    /// Whether this node's own result depends on a roll. A node whose whole subtree answers false gives the
    /// same answer every time, so it can be worked out before the query runs and any mistake in it reported
    /// before anything is rolled. Anything that touches <see cref="Context.Random"/> must answer true.
    /// </summary>
    bool Rolls { get; }

    /// <summary>Works out this node's value, which is always of <see cref="ValueType"/>.</summary>
    /// <exception cref="EvalException">Something in the query can't be worked out, like dividing by 0.</exception>
    object Evaluate(Context context);
}

/// <summary>
/// One step of a query, as a node in a tree. A node knows the type of the value it produces, so a tree
/// that has been built at all is a tree whose types already line up; there is no separate checking pass
/// to keep in step with <see cref="Evaluate"/>.
/// </summary>
public abstract record Node<T> : INode where T : notnull
{
    /// <inheritdoc/>
    public Type ValueType => typeof(T);

    /// <inheritdoc/>
    public abstract IReadOnlyList<INode> Children { get; }

    /// <inheritdoc/>
    public virtual bool Rolls => false;

    /// <inheritdoc cref="INode.Evaluate"/>
    public abstract T Evaluate(Context context);

    object INode.Evaluate(Context context) => Evaluate(context);
}
