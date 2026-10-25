using Rql.Nodes;

namespace Rql.Syntax;

/// <summary>
/// <code>
/// query      := statement { ';' statement } [';']
/// statement  := [ (IDENTIFIER | TEXT) ':' ] expression
/// expression := term { ('+' | '-') term }
/// term       := unary { ('*' | '/' | '%') unary }
/// unary      := '-' unary | power
/// power      := postfix [ '**' unary ]
/// postfix    := primary { LABEL }
/// primary    := NUMBER | TEXT | DICE | TABLEROLL | IDENTIFIER | call | '(' expression ')' | group
/// call       := IDENTIFIER '(' [ expression { ',' expression } ] ')'
/// group      := '{' expression { ',' expression } '}' [ GROUPMODIFIERS ]
/// </code>
/// An identifier can have dots in it, like <c>Monsters.Undead</c>, naming a table inside a folder. Dice are
/// in Roll20's notation; see <see cref="DiceNotation"/>. Roll20's order of operations is kept: brackets, then
/// functions, then <c>**</c>, then <c>* / %</c>, then <c>+ -</c>.
/// </summary>
internal sealed class Parser
{
    private readonly string source;
    private readonly IReadOnlyList<Token> tokens;
    private int current;

    private Parser(string source)
    {
        this.source = source;
        tokens = Lexer.Tokenize(source);
    }

    /// <summary>A whole query: one or more rolls, separated by semicolons and each perhaps labelled.</summary>
    public static IReadOnlyList<Statement> ParseQuery(string query)
    {
        var parser = new Parser(query);
        var statements = new List<Statement> { parser.Statement() };
        while (parser.Match(TokenType.Semicolon) && parser.Peek().Type != TokenType.End)
        {
            statements.Add(parser.Statement());
        }
        parser.ExpectEnd();
        return statements;
    }

    /// <summary>A single expression, with no labels or semicolons.</summary>
    public static Expr Parse(string query)
    {
        var parser = new Parser(query);
        Expr expr = parser.Expression();
        parser.ExpectEnd();
        return expr;
    }

    private void ExpectEnd()
    {
        Token next = Peek();
        if (next.Type != TokenType.End)
        {
            string expected = next.Type == TokenType.Colon
                ? "a label to come first in a roll, like Attack: d20"
                : "an operator, ';' or the end of the query";
            throw new QueryException($"expected {expected} but found {next.Describe()}", next.Position);
        }
    }

    private Statement Statement()
    {
        int start = Peek().Position;
        string? label = null;
        if (Peek().Type is TokenType.Identifier or TokenType.Text && Peek(1).Type == TokenType.Colon)
        {
            label = Advance().Text;
            Advance();
        }
        Expr expr = Expression();
        return new Statement(label, expr, start, Peek().Position);
    }

    private Expr Expression()
    {
        Expr expr = Term();
        while (Peek().Type is TokenType.Plus or TokenType.Minus)
        {
            Token op = Advance();
            expr = new Binary(op.Text, expr, Term(), op.Position);
        }
        return expr;
    }

    private Expr Term()
    {
        Expr expr = Unary();
        while (Peek().Type is TokenType.Star or TokenType.Slash or TokenType.Percent)
        {
            Token op = Advance();
            expr = new Binary(op.Text, expr, Unary(), op.Position);
        }
        return expr;
    }

    private Expr Unary()
    {
        if (Peek().Type != TokenType.Minus)
        {
            return Power();
        }
        Token minus = Advance();
        // A minus written straight onto a number is part of the number, unless the number is raised to a
        // power: -2**2 is -(2**2), as in mathematics.
        if (Peek() is { Type: TokenType.Number } number && number.Position == minus.Position + 1
            && Peek(1).Type is not (TokenType.Power or TokenType.Label))
        {
            Advance();
            return new NumberLiteral(Number("-" + number.Text, minus), minus.Position);
        }
        return new Negate(Unary(), minus.Position);
    }

    private Expr Power()
    {
        Expr expr = Postfix();
        if (Peek().Type == TokenType.Power)
        {
            Token op = Advance();
            // Raising to a power groups from the right, so 2**3**2 is 2**(3**2).
            expr = new Binary("**", expr, Unary(), op.Position);
        }
        return expr;
    }

    private Expr Postfix()
    {
        Expr expr = Primary();
        while (Peek().Type == TokenType.Label)
        {
            Token label = Advance();
            expr = new Labelled(expr, label.Text, label.Position);
        }
        return expr;
    }

    private Expr Primary()
    {
        Token token = Advance();
        switch (token.Type)
        {
            case TokenType.Number:
                return new NumberLiteral(Number(token.Text, token), token.Position);
            case TokenType.Text:
                return new TextLiteral(token.Text, token.Position);
            case TokenType.Dice:
                return Dice(token);
            case TokenType.TableRoll:
                return TableRoll(token);
            case TokenType.Identifier:
                return Peek().Type == TokenType.LeftParen ? Call(token) : new Identifier(token.Text, token.Position);
            case TokenType.LeftParen:
                Expr inner = Expression();
                Expect(TokenType.RightParen, "an operator or ')'");
                return inner;
            case TokenType.LeftBrace:
                return Group(token);
            default:
                throw new QueryException(
                    $"expected a number, text, dice, table, function call, '(' or '{{' but found {token.Describe()}",
                    token.Position);
        }
    }

    private Group Group(Token open)
    {
        var items = new List<Expr>();
        do
        {
            items.Add(Expression());
        } while (Match(TokenType.Comma));
        Token close = Expect(TokenType.RightBrace, "',' or '}'");
        int end = close.Position + 1;

        GroupModifiers modifiers = GroupModifiers.None;
        if (Peek().Type == TokenType.GroupModifiers)
        {
            Token written = Advance();
            try
            {
                modifiers = DiceNotation.ParseGroupModifiers(written.Text);
            }
            catch (DiceNotationException e)
            {
                throw new QueryException(e.Message, written.Position + e.Offset);
            }
            end = written.Position + written.Text.Length;
        }
        return new Group(items, modifiers, source[open.Position..end], open.Position);
    }

    private static DiceLiteral Dice(Token token)
    {
        try
        {
            return new DiceLiteral(DiceNotation.Parse(token.Text), token.Position);
        }
        catch (DiceNotationException e)
        {
            throw new QueryException(e.Message, token.Position + e.Offset);
        }
    }

    private static TableRoll TableRoll(Token token)
    {
        int t = token.Text.IndexOfAny(['t', 'T']);
        int count = t == 0 ? 1 : (int)Number(token.Text[..t], token);
        string name = token.Text[(t + 2)..^1].Trim();
        return name.Length == 0
            ? throw new QueryException("a table roll needs a table's name, like 1t[Flaws]", token.Position + t + 1)
            : new TableRoll(count, name, token.Position);
    }

    private Call Call(Token name)
    {
        Advance();
        var arguments = new List<Expr>();
        if (Peek().Type != TokenType.RightParen)
        {
            do
            {
                arguments.Add(Expression());
            } while (Match(TokenType.Comma));
        }
        Expect(TokenType.RightParen, "',' or ')'");
        return new Call(name.Text, arguments, name.Position);
    }

    private Token Expect(TokenType type, string expected)
    {
        Token token = Advance();
        return token.Type == type
            ? token
            : throw new QueryException($"expected {expected} but found {token.Describe()}", token.Position);
    }

    private static decimal Number(string text, Token token) =>
        Numbers.TryParse(text, out decimal value)
            ? value
            : throw new QueryException($"the number {text} is too large", token.Position);

    private bool Match(TokenType type)
    {
        if (Peek().Type == type)
        {
            Advance();
            return true;
        }
        return false;
    }

    private Token Peek(int ahead = 0) => tokens[Math.Min(current + ahead, tokens.Count - 1)];

    private Token Advance()
    {
        Token token = tokens[current];
        if (token.Type != TokenType.End)
        {
            current++;
        }
        return token;
    }
}
