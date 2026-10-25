namespace Rql.Projects;

/// <summary>A query kept under a name, so it can be run again without writing it out.</summary>
public sealed record SavedQuery(string Name, string Query);
