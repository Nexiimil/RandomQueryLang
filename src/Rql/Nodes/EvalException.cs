namespace Rql.Nodes;

/// <summary>
/// Something that went wrong while evaluating, blamed on the node responsible rather than on a position in
/// the text. The binder keeps the positions of the nodes it built, so it can turn the blamed node back
/// into a place in the query; a tree built by hand has no positions and keeps the message.
/// </summary>
public sealed class EvalException(INode blame, string message) : Exception(message)
{
    /// <summary>The node the error is about, which is usually one of the arguments rather than the function.</summary>
    public INode Blame { get; } = blame;
}
