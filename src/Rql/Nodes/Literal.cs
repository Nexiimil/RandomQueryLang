namespace Rql.Nodes;

/// <summary>
/// A value that was written into the query, or looked up while the tree was being built. Table names
/// become one of these once the binder has found the table, so nothing looks tables up while running.
/// </summary>
public sealed record Literal<T>(T Value) : Node<T> where T : notnull
{
    public override IReadOnlyList<INode> Children => [];

    public override T Evaluate(Context context) => Value;
}

public static class Literal
{
    public static Literal<T> Of<T>(T value) where T : notnull => new(value);

    /// <summary>A literal of a type only known while running, such as a parameter's default.</summary>
    public static INode Of(Type type, object value) =>
        (INode)Activator.CreateInstance(typeof(Literal<>).MakeGenericType(type), value)!;
}
