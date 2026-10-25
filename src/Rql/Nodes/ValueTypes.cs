using Rql.Tables;

namespace Rql.Nodes;

/// <summary>
/// The types a node can produce. A node's type is its <c>T</c>, so these are just how each one is named
/// in an error and shown to whoever ran the query.
/// </summary>
public static class ValueTypes
{
    /// <summary>Numbers, text, tables of things, and dice described rather than rolled.</summary>
    public static IReadOnlyList<Type> All { get; } = [typeof(decimal), typeof(string), typeof(Table), typeof(Dice)];

    public static bool IsValueType(Type type) => All.Contains(type);

    /// <summary>How a type is named in an error, such as "a number".</summary>
    public static string Describe(Type type) =>
        type == typeof(decimal) ? "a number"
        : type == typeof(string) ? "text"
        : type == typeof(Table) ? "a table"
        : type == typeof(Dice) ? "dice"
        : type.Name;

    /// <summary>How a value is shown to whoever ran the query.</summary>
    public static string Display(object value) => value switch
    {
        Table { Count: 0 } => "(no rows)",
        Table table => string.Join(Environment.NewLine, table.Things.Select(thing => $"{thing.Name} ({thing.Value})")),
        Dice dice => dice.Display(),
        decimal number => Numbers.Display(number),
        _ => value.ToString() ?? "",
    };
}
