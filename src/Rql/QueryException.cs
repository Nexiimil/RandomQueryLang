namespace Rql;

/// <summary>A problem with a query, pointing at the column of the query text where it was found.</summary>
public sealed class QueryException(string message, int position) : Exception(message)
{
    /// <summary>Where a problem has no place in the text, such as a default the query never wrote.</summary>
    public const int Nowhere = -1;

    public int Position { get; } = position;
}
