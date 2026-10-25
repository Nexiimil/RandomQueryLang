namespace Rql.Tables;

/// <summary>Rows ready to be rolled on, which is what every table-shaped result of a query is.</summary>
public sealed record Table(string Name, IReadOnlyList<Thing> Things)
{
    public IReadOnlyList<Thing> Things { get; } = [.. Things];

    public int Count => Things.Count;

    public decimal Total() => Things.Sum(thing => (decimal)thing.Value);

    public Table WithThings(IEnumerable<Thing> things) => new(Name, [.. things]);
}
