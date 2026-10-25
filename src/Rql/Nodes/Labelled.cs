namespace Rql.Nodes;

/// <summary>A term with a label after it, like <c>5[STR]</c>. The value goes through unchanged, and is logged under the label.</summary>
public sealed record Labelled<T>(Node<T> Inner, string Label) : Node<T> where T : notnull
{
    public override IReadOnlyList<INode> Children => [Inner];

    public override T Evaluate(Context context)
    {
        T value = Inner.Evaluate(context);
        context.Log.Add(new LabelledValue(Label, ValueTypes.Display(value)));
        return value;
    }
}

public static class Labelling
{
    /// <summary>Labels a node of any type.</summary>
    public static INode Label(INode inner, string label) =>
        (INode)Activator.CreateInstance(typeof(Labelled<>).MakeGenericType(inner.ValueType), inner, label)!;
}
