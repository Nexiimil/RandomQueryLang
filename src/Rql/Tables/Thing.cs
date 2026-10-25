namespace Rql.Tables;

/// <summary>One row of a table, with its value and chance worked out.</summary>
public sealed record Thing(string Name, int Value, double Chance);
