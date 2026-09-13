package rql.syntax;

import java.util.List;

/** A parsed query, before anything has been looked up or rolled. */
public sealed interface Expr {
    int position();

    record NumberLiteral(int value, int position) implements Expr {
    }

    record TextLiteral(String value, int position) implements Expr {
    }

    record DiceLiteral(int count, int sides, int position) implements Expr {
    }

    /** A bare name, which refers to a table. */
    record Identifier(String name, int position) implements Expr {
    }

    record Call(String name, List<Expr> arguments, int position) implements Expr {
        public Call {
            arguments = List.copyOf(arguments);
        }
    }
}
