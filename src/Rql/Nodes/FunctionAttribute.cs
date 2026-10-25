namespace Rql.Nodes;

/// <summary>Names the function a query calls to build this node.</summary>
/// <remarks>Several nodes can share a name, as Roll does; the binder picks by the type of the first argument.</remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FunctionAttribute(string name) : Attribute
{
    public string Name { get; } = name;

    /// <summary>What the function does, for whoever is writing a query.</summary>
    public string? Description { get; init; }
}

/// <summary>Makes an argument optional, standing in for it with this value when a query leaves it out.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class DefaultAttribute(object value) : Attribute
{
    public object Value { get; } = value;
}
