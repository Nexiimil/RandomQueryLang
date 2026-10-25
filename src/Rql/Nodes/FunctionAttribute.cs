namespace Rql.Nodes;

/// <summary>Names the function a query calls to build this node.</summary>
/// <remarks>Several nodes can share a name, as Roll does; the binder picks by the type of the first argument.</remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FunctionAttribute(string name) : Attribute
{
    /// <summary>The name a query calls the function by, in any case.</summary>
    public string Name { get; } = name;

    /// <summary>What the function does, for whoever is writing a query.</summary>
    public string? Description { get; init; }
}

/// <summary>Makes an argument optional, standing in for it with this value when a query leaves it out.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class DefaultAttribute(object value) : Attribute
{
    /// <summary>
    /// What stands in for the argument. A number's default can be written as a whole number or a double,
    /// since an attribute can't hold a decimal.
    /// </summary>
    public object Value { get; } = value;
}
