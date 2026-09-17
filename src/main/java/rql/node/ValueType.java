package rql.node;

import java.util.function.Function;
import java.util.stream.Collectors;

import rql.table.Table;

/**
 * The type of value a node produces. Instances are compared by identity, so the constants below are
 * the only ones there are.
 *
 * <p>This exists so the binder can compare types while the compiler tracks them: {@code ValueType<T>}
 * carries {@code T} through a tree whose nodes have different types from each other.
 */
public final class ValueType<T> {
    /** Whole numbers. */
    public static final ValueType<Integer> NUMBER =
            new ValueType<>("a number", Integer.class, Object::toString);

    /** Text, written in double quotes in a query. */
    public static final ValueType<String> TEXT = new ValueType<>("text", String.class, text -> text);

    /** A table of things, which is what rolling, picking and dropping all produce. */
    public static final ValueType<Table> TABLE =
            new ValueType<>("a table", Table.class, ValueType::showTable);

    /**
     * Dice such as 4d6, held as a description rather than a roll. Keeping them separate from tables is
     * what lets {@code Roll(4d6, 2)} roll four dice twice instead of picking two rows.
     */
    public static final ValueType<Dice> DICE = new ValueType<>("dice", Dice.class, Dice::display);

    private final String description;
    private final Class<T> carrier;
    private final Function<T, String> display;

    private ValueType(String description, Class<T> carrier, Function<T, String> display) {
        this.description = description;
        this.carrier = carrier;
        this.display = display;
    }

    /** How this type is named in an error, such as "a number". */
    public String description() {
        return description;
    }

    /** Lets a value of this type be recovered from an {@code Object} without an unchecked cast. */
    public T cast(Object value) {
        return carrier.cast(value);
    }

    /** How a value of this type is shown to whoever ran the query. */
    public String display(T value) {
        return display.apply(value);
    }

    @Override
    public String toString() {
        return description;
    }

    private static String showTable(Table table) {
        if (table.things().isEmpty()) {
            return "(no rows)";
        }
        return table.things().stream()
                .map(thing -> thing.name() + " (" + thing.value() + ")")
                .collect(Collectors.joining(System.lineSeparator()));
    }
}
