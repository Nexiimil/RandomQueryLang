package rql.syntax;

/**
 * A piece of a query. For {@link TokenType#TEXT} tokens, {@code text} is the content between the
 * quotes with escapes already applied.
 */
public record Token(TokenType type, String text, int position) {
    public String describe() {
        return switch (type) {
            case END -> "end of query";
            case TEXT -> "text \"" + text + "\"";
            default -> "'" + text + "'";
        };
    }
}
