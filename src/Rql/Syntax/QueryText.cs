using Rql.Tables;

namespace Rql.Syntax;

/// <summary>Writing values back out as query text, for anything that builds queries for someone.</summary>
public static class QueryText
{
    /// <summary>
    /// How to refer to a table in a query: its bare name where that reads back as the table, like
    /// <c>Monsters.Undead</c>, or else in quotes, which is how a name with spaces, or one that looks like
    /// dice, is written.
    /// </summary>
    public static string TableReference(string name)
    {
        var tokens = Lexer.TryTokenize(name);
        bool bare = tokens is [{ Type: TokenType.Identifier } only, { Type: TokenType.End }] && only.Text == name;
        return bare ? name : Quote(name);
    }

    /// <summary>The shortest way to refer to a table among the others, so a nested table reads as its own name where it can.</summary>
    public static string TableReference(TableCatalog tables, string fullName) =>
        TableReference(tables.ShortestName(fullName));

    /// <summary>Text in quotes, escaped so it reads back as itself.</summary>
    public static string Quote(string text) => "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
