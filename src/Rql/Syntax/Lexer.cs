namespace Rql.Syntax;

internal sealed class Lexer
{
    private readonly string source;
    private int position;

    private Lexer(string source)
    {
        this.source = source;
    }

    public static IReadOnlyList<Token> Tokenize(string source) => new Lexer(source).Tokenize();

    /// <summary>The tokens of <paramref name="source"/>, or null where it isn't made of tokens.</summary>
    public static IReadOnlyList<Token>? TryTokenize(string source)
    {
        try
        {
            return Tokenize(source);
        }
        catch (QueryException)
        {
            return null;
        }
    }

    private List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            while (position < source.Length && char.IsWhiteSpace(source[position]))
            {
                position++;
            }
            if (position >= source.Length)
            {
                tokens.Add(new Token(TokenType.End, "", position));
                return tokens;
            }
            tokens.Add(Next());
            if (tokens[^1].Type == TokenType.RightBrace && GroupModifiers() is Token modifiers)
            {
                tokens.Add(modifiers);
            }
        }
    }

    private Token Next()
    {
        char c = source[position];
        switch (c)
        {
            case '(': return Symbol(TokenType.LeftParen);
            case ')': return Symbol(TokenType.RightParen);
            case '{': return Symbol(TokenType.LeftBrace);
            case '}': return Symbol(TokenType.RightBrace);
            case ',': return Symbol(TokenType.Comma);
            case '+': return Symbol(TokenType.Plus);
            case '-': return Symbol(TokenType.Minus);
            case '/': return Symbol(TokenType.Slash);
            case '%': return Symbol(TokenType.Percent);
            case ';': return Symbol(TokenType.Semicolon);
            case ':': return Symbol(TokenType.Colon);
            case '"': return Text();
            case '[': return Label();
            case '*':
                if (At(position + 1) == '*')
                {
                    position += 2;
                    return new Token(TokenType.Power, "**", position - 2);
                }
                return Symbol(TokenType.Star);
        }
        if (IsDigit(c))
        {
            return Number();
        }
        if (IsWordStart(c))
        {
            return Word();
        }
        string hint = c switch
        {
            '@' => " (@{...} is a character sheet value, asked for when the query is run)",
            '?' => " (a roll query is written ?{Question|default})",
            '#' => " (a macro is written #Name, naming a saved query)",
            _ => "",
        };
        throw new QueryException($"unexpected character '{c}'{hint}", position);
    }

    private Token Symbol(TokenType type)
    {
        var token = new Token(type, source[position].ToString(), position);
        position++;
        return token;
    }

    /// <summary>
    /// A number, which can have a decimal point, or a count in front of dice (<c>4d6</c>) or of a table
    /// roll (<c>2t[Flaws]</c>).
    /// </summary>
    private Token Number()
    {
        int start = position;
        int dice = DiceNotation.EndWithCount(source, start);
        if (dice > start)
        {
            position = dice;
            return Token(TokenType.Dice, start);
        }
        while (IsDigitAt(position))
        {
            position++;
        }
        if (At(position) is 't' or 'T' && At(position + 1) == '[')
        {
            return TableRoll(start);
        }
        if (At(position) == '.' && IsDigitAt(position + 1))
        {
            position++;
            while (IsDigitAt(position))
            {
                position++;
            }
        }
        return Token(TokenType.Number, start);
    }

    // A table in a folder is written with dots between the folders, like Monsters.Undead.
    private Token Word()
    {
        int start = position;
        int dice = DiceNotation.End(source, start);
        if (dice > start)
        {
            position = dice;
            return Token(TokenType.Dice, start);
        }
        if (At(position) is 't' or 'T' && At(position + 1) == '[')
        {
            return TableRoll(start);
        }
        position = WordEnd(position);
        while (position + 1 < source.Length && source[position] == '.' && IsWordStart(source[position + 1]))
        {
            position = WordEnd(position + 1);
        }
        return Token(TokenType.Identifier, start);
    }

    /// <summary>A table roll, from its count or <c>t</c> through to the closing <c>]</c>.</summary>
    private Token TableRoll(int start)
    {
        int close = source.IndexOf(']', position);
        if (close < 0)
        {
            throw new QueryException("the table's name is missing its closing ]", position + 1);
        }
        position = close + 1;
        return Token(TokenType.TableRoll, start);
    }

    private Token Label()
    {
        int start = position;
        if (At(position + 1) == '[')
        {
            throw new QueryException("an inline roll [[ ]] is missing its closing ]]", start);
        }
        int close = source.IndexOf(']', position);
        if (close < 0)
        {
            throw new QueryException("the label is missing its closing ]", start);
        }
        position = close + 1;
        return new Token(TokenType.Label, source[(start + 1)..close].Trim(), start);
    }

    /// <summary>Keep, drop and success modifiers written straight after a group's closing brace.</summary>
    private Token? GroupModifiers()
    {
        int end = DiceNotation.ModifiersEnd(source, position);
        if (end <= position)
        {
            return null;
        }
        int start = position;
        position = end;
        return Token(TokenType.GroupModifiers, start);
    }

    private static bool IsWordStart(char c) => char.IsLetter(c) || c == '_';

    private int WordEnd(int index)
    {
        while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] == '_'))
        {
            index++;
        }
        return index;
    }

    private Token Token(TokenType type, int start) => new(type, source[start..position], start);

    private Token Text()
    {
        int start = position;
        position++;
        var text = new System.Text.StringBuilder();
        while (position < source.Length)
        {
            char c = source[position++];
            if (c == '"')
            {
                return new Token(TokenType.Text, text.ToString(), start);
            }
            if (c == '\\')
            {
                if (position >= source.Length)
                {
                    break;
                }
                c = source[position++];
                if (c != '"' && c != '\\')
                {
                    throw new QueryException($"unknown escape \\{c} (only \\\" and \\\\ are allowed)", position - 2);
                }
            }
            text.Append(c);
        }
        throw new QueryException("text is missing its closing \"", start);
    }

    private char At(int index) => index < source.Length ? source[index] : '\0';

    private bool IsDigitAt(int index) => index < source.Length && IsDigit(source[index]);

    private static bool IsDigit(char c) => c is >= '0' and <= '9';
}
