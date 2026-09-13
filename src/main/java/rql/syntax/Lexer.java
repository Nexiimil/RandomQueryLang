package rql.syntax;

import java.util.ArrayList;
import java.util.List;

import rql.QueryException;

public final class Lexer {
    private final String source;
    private int position = 0;

    private Lexer(String source) {
        this.source = source;
    }

    public static List<Token> tokenize(String source) {
        return new Lexer(source).tokenize();
    }

    private List<Token> tokenize() {
        List<Token> tokens = new ArrayList<>();
        while (true) {
            while (position < source.length() && Character.isWhitespace(source.charAt(position))) {
                position++;
            }
            if (position >= source.length()) {
                tokens.add(new Token(TokenType.END, "", position));
                return tokens;
            }
            tokens.add(next());
        }
    }

    private Token next() {
        char c = source.charAt(position);
        return switch (c) {
            case '(' -> symbol(TokenType.LEFT_PAREN);
            case ')' -> symbol(TokenType.RIGHT_PAREN);
            case ',' -> symbol(TokenType.COMMA);
            case '"' -> text();
            default -> {
                if (isDigit(c) || (c == '-' && isDigitAt(position + 1))) {
                    yield number();
                }
                if (Character.isLetter(c) || c == '_') {
                    yield word();
                }
                throw new QueryException("unexpected character '" + c + "'", position);
            }
        };
    }

    private Token symbol(TokenType type) {
        Token token = new Token(type, String.valueOf(source.charAt(position)), position);
        position++;
        return token;
    }

    // A number directly followed by a die, as in 4d6, is how many of that die to roll.
    private Token number() {
        int start = position;
        if (source.charAt(position) == '-') {
            position++;
        }
        while (isDigitAt(position)) {
            position++;
        }
        int end = wordEnd(position);
        if (isDie(position, end)) {
            position = end;
            return token(TokenType.DICE, start);
        }
        return token(TokenType.NUMBER, start);
    }

    private Token word() {
        int start = position;
        position = wordEnd(position);
        return token(isDie(start, position) ? TokenType.DICE : TokenType.IDENTIFIER, start);
    }

    private int wordEnd(int index) {
        while (index < source.length()
                && (Character.isLetterOrDigit(source.charAt(index)) || source.charAt(index) == '_')) {
            index++;
        }
        return index;
    }

    // Whether the word from start to end is a die such as d6 or D20.
    private boolean isDie(int start, int end) {
        if (end - start < 2 || Character.toLowerCase(source.charAt(start)) != 'd') {
            return false;
        }
        for (int i = start + 1; i < end; i++) {
            if (!isDigit(source.charAt(i))) {
                return false;
            }
        }
        return true;
    }

    private Token token(TokenType type, int start) {
        return new Token(type, source.substring(start, position), start);
    }

    private Token text() {
        int start = position;
        position++;
        StringBuilder text = new StringBuilder();
        while (position < source.length()) {
            char c = source.charAt(position++);
            if (c == '"') {
                return new Token(TokenType.TEXT, text.toString(), start);
            }
            if (c == '\\') {
                if (position >= source.length()) {
                    break;
                }
                c = source.charAt(position++);
                if (c != '"' && c != '\\') {
                    throw new QueryException(
                            "unknown escape \\" + c + " (only \\\" and \\\\ are allowed)", position - 2);
                }
            }
            text.append(c);
        }
        throw new QueryException("text is missing its closing \"", start);
    }

    private boolean isDigitAt(int index) {
        return index < source.length() && isDigit(source.charAt(index));
    }

    private static boolean isDigit(char c) {
        return c >= '0' && c <= '9';
    }
}
