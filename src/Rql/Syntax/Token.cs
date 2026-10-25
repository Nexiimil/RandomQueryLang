namespace Rql.Syntax;

/// <summary>
/// A piece of a query. For <see cref="TokenType.Text"/> tokens, <c>Text</c> is the content between the
/// quotes with escapes already applied.
/// </summary>
public sealed record Token(TokenType Type, string Text, int Position)
{
    public string Describe() => Type switch
    {
        TokenType.End => "end of query",
        TokenType.Text => $"text \"{Text}\"",
        _ => $"'{Text}'",
    };
}
