package rql.syntax;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

import rql.QueryException;

/**
 * <pre>
 * expression := NUMBER | TEXT | DICE | IDENTIFIER | call
 * call       := IDENTIFIER '(' [ expression { ',' expression } ] ')'
 * </pre>
 */
public final class Parser {
    private final List<Token> tokens;
    private int current = 0;

    private Parser(List<Token> tokens) {
        this.tokens = tokens;
    }

    public static Expr parse(String query) {
        Parser parser = new Parser(Lexer.tokenize(query));
        Expr expr = parser.expression();
        Token next = parser.peek();
        if (next.type() != TokenType.END) {
            throw new QueryException("expected end of query but found " + next.describe(), next.position());
        }
        return expr;
    }

    private Expr expression() {
        Token token = advance();
        return switch (token.type()) {
            case NUMBER -> new Expr.NumberLiteral(wholeNumber(token.text(), token), token.position());
            case TEXT -> new Expr.TextLiteral(token.text(), token.position());
            case DICE -> dice(token);
            case IDENTIFIER -> peek().type() == TokenType.LEFT_PAREN
                    ? call(token)
                    : new Expr.Identifier(token.text(), token.position());
            default -> throw new QueryException(
                    "expected a number, text, dice, table or function call but found " + token.describe(),
                    token.position());
        };
    }

    private Expr dice(Token token) {
        String text = token.text();
        int d = text.toLowerCase(Locale.ROOT).indexOf('d');
        int count = d == 0 ? 1 : wholeNumber(text.substring(0, d), token);
        int sides = wholeNumber(text.substring(d + 1), token);
        if (count < 1) {
            throw new QueryException("can't roll " + count + " dice", token.position());
        }
        if (sides < 1) {
            throw new QueryException("a die needs at least 1 side", token.position());
        }
        return new Expr.DiceLiteral(count, sides, token.position());
    }

    private Expr call(Token name) {
        advance();
        List<Expr> arguments = new ArrayList<>();
        if (peek().type() != TokenType.RIGHT_PAREN) {
            do {
                arguments.add(expression());
            } while (match(TokenType.COMMA));
        }
        Token closing = advance();
        if (closing.type() != TokenType.RIGHT_PAREN) {
            throw new QueryException("expected ',' or ')' but found " + closing.describe(), closing.position());
        }
        return new Expr.Call(name.text(), arguments, name.position());
    }

    private static int wholeNumber(String text, Token token) {
        try {
            return Integer.parseInt(text);
        } catch (NumberFormatException e) {
            throw new QueryException("the number " + text + " is too large", token.position());
        }
    }

    private boolean match(TokenType type) {
        if (peek().type() == type) {
            advance();
            return true;
        }
        return false;
    }

    private Token peek() {
        return tokens.get(current);
    }

    private Token advance() {
        Token token = tokens.get(current);
        if (token.type() != TokenType.END) {
            current++;
        }
        return token;
    }
}
