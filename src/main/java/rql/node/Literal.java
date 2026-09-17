package rql.node;

import java.util.List;

import rql.table.Table;

/**
 * A value that was written into the query, or looked up while the tree was being built. Table names
 * become one of these once the binder has found the table, so nothing looks tables up while running.
 */
public record Literal<T>(ValueType<T> type, T value) implements Node<T> {
    @Override
    public T evaluate(Context context) {
        return value;
    }

    @Override
    public List<Node<?>> children() {
        return List.of();
    }

    public static Literal<Integer> of(int number) {
        return new Literal<>(ValueType.NUMBER, number);
    }

    public static Literal<String> of(String text) {
        return new Literal<>(ValueType.TEXT, text);
    }

    public static Literal<Table> of(Table table) {
        return new Literal<>(ValueType.TABLE, table);
    }

    public static Literal<Dice> of(Dice dice) {
        return new Literal<>(ValueType.DICE, dice);
    }
}
