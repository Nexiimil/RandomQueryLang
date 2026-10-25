using System.Collections.Concurrent;
using System.Reflection;

namespace Rql.Nodes;

/// <summary>One argument of a function, as a query writes it.</summary>
public sealed record Parameter(string Name, Type Type, object? Default)
{
    public bool IsOptional => Default is not null;

    /// <summary>What this parameter stands for when the query leaves it out.</summary>
    public INode DefaultNode() =>
        Literal.Of(Type, Default ?? throw new InvalidOperationException($"{Name} is required and has no default"));

    public override string ToString() => IsOptional ? $"[{Name}]" : Name;
}

/// <summary>
/// How a function is written in a query, and how to build its node from what was written. Read from the
/// node's constructor, whose parameters must each be a <see cref="Node{T}"/> of one of the
/// <see cref="ValueTypes"/>; anything else is reported when the function is registered, not when a query
/// first uses it.
/// </summary>
public sealed class Signature
{
    private static readonly ConcurrentDictionary<Type, Signature> Cache = new();

    private readonly ConstructorInfo constructor;
    private readonly PropertyInfo[] properties;

    private Signature(Type node, FunctionAttribute function, Type returns, ConstructorInfo constructor,
        IReadOnlyList<Parameter> parameters, PropertyInfo[] properties)
    {
        NodeType = node;
        Name = function.Name;
        Description = function.Description;
        Returns = returns;
        Parameters = parameters;
        this.constructor = constructor;
        this.properties = properties;
    }

    public string Name { get; }

    public string? Description { get; }

    /// <summary>The record built when a query calls this.</summary>
    public Type NodeType { get; }

    public Type Returns { get; }

    public IReadOnlyList<Parameter> Parameters { get; }

    /// <summary>How many arguments a query has to give, the rest having defaults.</summary>
    public int Required => Parameters.Count(parameter => !parameter.IsOptional);

    /// <summary>How this function is written, for an error that says what was expected.</summary>
    public string Usage => $"{Name}({string.Join(", ", Parameters)})";

    public static Signature Of<TFunction>() where TFunction : INode => Of(typeof(TFunction));

    /// <exception cref="ArgumentException">The type can't be called as a function.</exception>
    public static Signature Of(Type node) => Cache.GetOrAdd(node, Read);

    /// <summary>
    /// Builds the node, filling in the defaults of any parameters the query left out. Arguments must
    /// already be the right types; the binder is what reports a query that got them wrong, so anything
    /// still wrong here is a mistake in the binder rather than in the query.
    /// </summary>
    public INode Build(IReadOnlyList<INode> arguments)
    {
        if (arguments.Count < Required || arguments.Count > Parameters.Count)
        {
            throw new ArgumentException($"expected {Usage} but got {arguments.Count} arguments");
        }

        var complete = new object[Parameters.Count];
        for (int i = 0; i < Parameters.Count; i++)
        {
            INode argument = i < arguments.Count ? arguments[i] : Parameters[i].DefaultNode();
            if (argument.ValueType != Parameters[i].Type)
            {
                throw new ArgumentException($"{Name} wanted {ValueTypes.Describe(Parameters[i].Type)} for "
                    + $"{Parameters[i].Name} but was given {ValueTypes.Describe(argument.ValueType)}");
            }
            complete[i] = argument;
        }
        return (INode)constructor.Invoke(complete);
    }

    /// <summary>A node's arguments, read back from the properties its constructor set.</summary>
    internal IReadOnlyList<INode> ArgumentsOf(INode node) =>
        [.. properties.Select(property => (INode)property.GetValue(node)!)];

    private static Signature Read(Type node)
    {
        string Problem(string what) => $"{node.Name} can't be used as a function: {what}";

        var function = node.GetCustomAttribute<FunctionAttribute>(inherit: false)
            ?? throw new ArgumentException(Problem("it has no [Function] attribute"));
        if (node.IsAbstract || node.IsGenericTypeDefinition)
        {
            throw new ArgumentException(Problem("it must be a concrete type"));
        }
        Type returns = ReturnTypeOf(node) ?? throw new ArgumentException(Problem("it must derive from Function<T>"));

        // A record also has a protected constructor that copies it, which isn't how a query builds one.
        ConstructorInfo[] constructors = node.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        if (constructors.Length != 1)
        {
            throw new ArgumentException(Problem($"it needs exactly one public constructor, not {constructors.Length}"));
        }
        ConstructorInfo constructor = constructors[0];

        var parameters = new List<Parameter>();
        var properties = new List<PropertyInfo>();
        foreach (ParameterInfo parameter in constructor.GetParameters())
        {
            string name = parameter.Name!;
            Type type = parameter.ParameterType;
            if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(Node<>)
                || !ValueTypes.IsValueType(type.GetGenericArguments()[0]))
            {
                throw new ArgumentException(Problem(
                    $"parameter {name} is {type.Name}, but every parameter must be a Node of a number, text, a table or dice"));
            }
            Type valueType = type.GetGenericArguments()[0];

            object? fallback = parameter.GetCustomAttribute<DefaultAttribute>()?.Value;
            // An attribute can't hold a decimal, so a number's default is written as a whole number or double.
            if (valueType == typeof(decimal) && fallback is int or long or double)
            {
                fallback = Convert.ToDecimal(fallback, System.Globalization.CultureInfo.InvariantCulture);
            }
            if (fallback is not null && !valueType.IsInstanceOfType(fallback))
            {
                throw new ArgumentException(Problem(
                    $"the default for {name} is {fallback.GetType().Name}, not {valueType.Name}"));
            }
            if (fallback is null && parameters.Any(earlier => earlier.IsOptional))
            {
                throw new ArgumentException(Problem($"{name} is required but comes after an optional parameter"));
            }

            properties.Add(node.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
                is { CanRead: true } property && property.PropertyType == type
                ? property
                : throw new ArgumentException(Problem($"it needs a public {name} property for its argument")));
            parameters.Add(new Parameter(Lowercase(name), valueType, fallback));
        }
        return new Signature(node, function, returns, constructor, parameters, [.. properties]);
    }

    private static Type? ReturnTypeOf(Type node)
    {
        for (Type? type = node; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Function<>))
            {
                return type.GetGenericArguments()[0];
            }
        }
        return null;
    }

    // Records name their arguments like properties, but a query is written about "table", not "Table".
    private static string Lowercase(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
