namespace Rql.Nodes;

/// <summary>
/// A node a query can call by name. Its constructor's parameters are its arguments, named and typed, so
/// writing the record is all there is to declaring one:
/// <code>
/// [Function("Roll")]
/// public sealed record RollTable(Node&lt;Table&gt; Table, [Default(1)] Node&lt;decimal&gt; Count) : Function&lt;Table&gt;
/// </code>
/// <see cref="Signature"/> reads the constructor to learn how the function is written, so there is no
/// parameter list kept beside the record to fall out of step with it.
/// </summary>
public abstract record Function<T> : Node<T> where T : notnull
{
    /// <summary>The arguments, in the order they're written.</summary>
    public sealed override IReadOnlyList<INode> Children => Signature.Of(GetType()).ArgumentsOf(this);
}
