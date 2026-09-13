package rql.functions;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import rql.value.Type;
import rql.value.Value;

/**
 * A function parameter. Values of any type in {@code types} are passed through as they are, and
 * anything else is converted to the first of them.
 */
public record Param(String name, List<Type> types, Value defaultValue) {
    public Param {
        types = List.copyOf(types);
    }

    public static Param required(String name, Type type, Type... alsoAccepted) {
        List<Type> types = new ArrayList<>(List.of(type));
        Collections.addAll(types, alsoAccepted);
        return new Param(name, types, null);
    }

    public static Param optional(String name, Type type, Value defaultValue) {
        return new Param(name, List.of(type), defaultValue);
    }

    /** The type that values this parameter doesn't accept are converted to. */
    public Type type() {
        return types.getFirst();
    }

    public boolean accepts(Type type) {
        return types.contains(type);
    }

    public boolean isOptional() {
        return defaultValue != null;
    }
}
