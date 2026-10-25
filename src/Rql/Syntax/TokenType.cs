namespace Rql.Syntax;

public enum TokenType
{
    Identifier,
    Number,
    Text,
    Dice,

    /// <summary>A roll on a table in Roll20's notation, <c>2t[Flaws]</c>; the text is the whole of it.</summary>
    TableRoll,

    /// <summary>A label on a term, <c>[STR]</c>; the text is what's inside the brackets.</summary>
    Label,

    /// <summary>What comes straight after a group's closing brace, like the <c>kh1</c> in <c>{d20, d20}kh1</c>.</summary>
    GroupModifiers,

    LeftParen,
    RightParen,
    LeftBrace,
    RightBrace,
    Comma,
    Plus,
    Minus,
    Star,
    Power,
    Slash,
    Percent,
    Semicolon,
    Colon,
    End,
}
