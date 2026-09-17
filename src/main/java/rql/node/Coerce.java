package rql.node;

import java.util.List;

import rql.table.Table;

/**
 * Converting a value to the type a function needs, as nodes in the tree rather than something the
 * evaluator does on the way past. Which conversion is needed is known when the tree is built, so each
 * one is its own node and none of them has to ask what it was given.
 *
 * <p>Text can stand in for a number when it is one, or for a table by naming it. A table with one row
 * can stand in for that row's value or name. Dice are rolled, giving a row per die where a table is
 * needed, or their total where a number is.
 */
public final class Coerce {
    private Coerce() {
    }

    /** Whether a value of one type can ever stand in for the other. A number is never a table. */
    public static boolean possible(ValueType<?> from, ValueType<?> to) {
        if (from == to) {
            return true;
        }
        if (to == ValueType.DICE) {
            return false;
        }
        return !(from == ValueType.NUMBER && to == ValueType.TABLE);
    }

    /**
     * Wraps {@code source} in whichever conversion reaches {@code wanted}. The binder calls
     * {@link #possible} first, so a pair that can never work is a mistake in the binder rather than in
     * the query.
     */
    public static <T> Node<T> to(ValueType<T> wanted, Node<?> source) {
        if (source.type() == wanted) {
            return cast(source);
        }
        if (wanted == ValueType.NUMBER) {
            return cast(toNumber(source));
        }
        if (wanted == ValueType.TEXT) {
            return cast(toText(source));
        }
        if (wanted == ValueType.TABLE) {
            return cast(toTable(source));
        }
        throw new IllegalStateException("nothing converts to " + wanted);
    }

    private static Node<Integer> toNumber(Node<?> source) {
        if (source.type() == ValueType.TEXT) {
            return new TextToNumber(cast(source));
        }
        if (source.type() == ValueType.TABLE) {
            return new TableToNumber(cast(source));
        }
        if (source.type() == ValueType.DICE) {
            return new DiceToNumber(cast(source));
        }
        throw new IllegalStateException("nothing converts " + source.type() + " to a number");
    }

    private static Node<String> toText(Node<?> source) {
        if (source.type() == ValueType.NUMBER) {
            return new NumberToText(cast(source));
        }
        if (source.type() == ValueType.TABLE) {
            return new TableToText(cast(source));
        }
        if (source.type() == ValueType.DICE) {
            return new DiceToText(cast(source));
        }
        throw new IllegalStateException("nothing converts " + source.type() + " to text");
    }

    private static Node<Table> toTable(Node<?> source) {
        if (source.type() == ValueType.TEXT) {
            return new TextToTable(cast(source));
        }
        if (source.type() == ValueType.DICE) {
            return new DiceToTable(cast(source));
        }
        throw new IllegalStateException("nothing converts " + source.type() + " to a table");
    }

    /** Safe because every caller has just compared {@link Node#type()} against the type it wants. */
    @SuppressWarnings("unchecked")
    private static <T> Node<T> cast(Node<?> source) {
        return (Node<T>) source;
    }

    private static int total(Table table, Node<?> blame) {
        long total = table.total();
        if (total < Integer.MIN_VALUE || total > Integer.MAX_VALUE) {
            throw new EvalException(blame, "the total " + total + " is too large");
        }
        return (int) total;
    }

    /** Text that spells a whole number, like {@code Roll(d6, "3")}. */
    public record TextToNumber(Node<String> source) implements Node<Integer> {
        @Override
        public ValueType<Integer> type() {
            return ValueType.NUMBER;
        }

        @Override
        public List<Node<?>> children() {
            return List.of(source);
        }

        @Override
        public Integer evaluate(Context context) {
            String text = source.evaluate(context);
            try {
                return Integer.parseInt(text.trim());
            } catch (NumberFormatException e) {
                throw new EvalException(source, "expected a number but got the text \"" + text + "\"");
            }
        }
    }

    /** A table of one row, standing in for that row's value. */
    public record TableToNumber(Node<Table> source) implements Node<Integer> {
        @Override
        public ValueType<Integer> type() {
            return ValueType.NUMBER;
        }

        @Override
        public List<Node<?>> children() {
            return List.of(source);
        }

        @Override
        public Integer evaluate(Context context) {
            Table table = source.evaluate(context);
            if (table.size() != 1) {
                throw new EvalException(source, "expected a number but got a table with " + table.size()
                        + " rows (use Sum to add them up)");
            }
            return table.things().getFirst().value();
        }
    }

    /** Dice standing in for a number, rolled and added up. */
    public record DiceToNumber(Node<Dice> source) implements Node<Integer> {
        @Override
        public ValueType<Integer> type() {
            return ValueType.NUMBER;
        }

        @Override
        public List<Node<?>> children() {
            return List.of(source);
        }

        @Override
        public boolean rolls() {
            return true;
        }

        @Override
        public Integer evaluate(Context context) {
            return total(source.evaluate(context).roll(context.random()), source);
        }
    }

    /** A number written where text was wanted. */
    public record NumberToText(Node<Integer> source) implements Node<String> {
        @Override
        public ValueType<String> type() {
            return ValueType.TEXT;
        }

        @Override
        public List<Node<?>> children() {
            return List.of(source);
        }

        @Override
        public String evaluate(Context context) {
            return Integer.toString(source.evaluate(context));
        }
    }

    /** A table of one row, standing in for that row's name. */
    public record TableToText(Node<Table> source) implements Node<String> {
        @Override
        public ValueType<String> type() {
            return ValueType.TEXT;
        }

        @Override
        public List<Node<?>> children() {
            return List.of(source);
        }

        @Override
        public String evaluate(Context context) {
            Table table = source.evaluate(context);
            if (table.size() != 1) {
                throw new EvalException(
                        source, "expected text but got a table with " + table.size() + " rows");
            }
            return table.things().getFirst().name();
        }
    }

    /** Dice standing in for text, rolled and added up. */
    public record DiceToText(Node<Dice> source) implements Node<String> {
        @Override
        public ValueType<String> type() {
            return ValueType.TEXT;
        }

        @Override
        public List<Node<?>> children() {
            return List.of(source);
        }

        @Override
        public boolean rolls() {
            return true;
        }

        @Override
        public String evaluate(Context context) {
            return Integer.toString(total(source.evaluate(context).roll(context.random()), source));
        }
    }

    /** Text naming a table, which is how a name with spaces is written. */
    public record TextToTable(Node<String> source) implements Node<Table> {
        @Override
        public ValueType<Table> type() {
            return ValueType.TABLE;
        }

        @Override
        public List<Node<?>> children() {
            return List.of(source);
        }

        @Override
        public Table evaluate(Context context) {
            String name = source.evaluate(context);
            Table table = context.tables().get(name);
            if (table == null) {
                throw new EvalException(source, "no table named \"" + name + "\"");
            }
            return table;
        }
    }

    /** Dice used as a table, giving a row per die worth the number it landed on. */
    public record DiceToTable(Node<Dice> source) implements Node<Table> {
        @Override
        public ValueType<Table> type() {
            return ValueType.TABLE;
        }

        @Override
        public List<Node<?>> children() {
            return List.of(source);
        }

        @Override
        public boolean rolls() {
            return true;
        }

        @Override
        public Table evaluate(Context context) {
            return source.evaluate(context).roll(context.random());
        }
    }
}
