namespace Rql.Tables;

/// <summary>Rows ready to be rolled on, which is what every table-shaped result of a query is.</summary>
/// <param name="Name">The table's full name, folders and all.</param>
/// <param name="Things">The rows, in order.</param>
public sealed record Table(string Name, IReadOnlyList<Thing> Things)
{
    /// <summary>The rows, copied so the table doesn't change if the list it was made from does.</summary>
    public IReadOnlyList<Thing> Things { get; } = [.. Things];

    /// <summary>How many rows the table has.</summary>
    public int Count => Things.Count;

    /// <summary>Every row's value added up.</summary>
    public decimal Total() => Things.Sum(thing => (decimal)thing.Value);

    /// <summary>A table of the same name with these rows instead.</summary>
    public Table WithThings(IEnumerable<Thing> things) => new(Name, [.. things]);
}
