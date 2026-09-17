package rql.node;

/**
 * One argument of a function, as the query writes it. This is the only place a function's arguments
 * are a list: it exists to get from the positions in {@code Roll(loot, 3)} to the named components of
 * a node, and nothing downstream of the binder uses it.
 */
public record Param<T>(String name, ValueType<T> type, T defaultValue) {
    public static <T> Param<T> required(String name, ValueType<T> type) {
        return new Param<>(name, type, null);
    }

    public static <T> Param<T> optional(String name, ValueType<T> type, T defaultValue) {
        if (defaultValue == null) {
            throw new IllegalArgumentException("optional parameter " + name + " needs a default");
        }
        return new Param<>(name, type, defaultValue);
    }

    public boolean isOptional() {
        return defaultValue != null;
    }

    /** What this parameter stands for when the query leaves it out. */
    public Node<T> defaultNode() {
        if (defaultValue == null) {
            throw new IllegalStateException(name + " is required and has no default");
        }
        return new Literal<>(type, defaultValue);
    }

    @Override
    public String toString() {
        return isOptional() ? "[" + name + "]" : name;
    }
}
