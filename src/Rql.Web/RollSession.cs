using Rql.Evaluation;

namespace Rql.Web;

/// <summary>What someone has been rolling, kept for as long as their browser tab is connected.</summary>
public sealed class RollSession
{
    public string Query { get; set; } = "";

    /// <summary>Answers given to roll queries and character sheet values, offered again next time they're asked.</summary>
    public Dictionary<string, string> Answers { get; } = new(StringComparer.Ordinal);

    /// <summary>Newest first.</summary>
    public List<RollEntry> History { get; } = [];
}

/// <summary>
/// One query that was run, and either what it came to or why it couldn't be run. <c>SavedAs</c> is the
/// name of the saved query it was run from, if it was.
/// </summary>
public sealed record RollEntry(string Query, QueryResult? Result, QueryException? Error, string? SavedAs = null)
{
    public Guid Id { get; } = Guid.NewGuid();
}
