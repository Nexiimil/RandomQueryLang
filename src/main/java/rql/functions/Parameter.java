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
public record Parameter(String name, Kind kind, List<Type> types, Value defaultValue) {
    public enum Kind {
        /** An argument that must be given. */
        REQUIRED,
        /** An argument that can be left out, in which case the default value is used. */
        OPTIONAL,
        /** Any number of arguments, including none. Only the last parameter can be repeated. */
        REPEATED,
        /** An argument that must be given, and is passed to the function as a {@link Query} without being run. */
        QUERY
    }

    public Parameter {
        types = List.copyOf(types);
    }

    public static Parameter required(String name, Type type, Type... alsoAccepted) {
        return new Parameter(name, Kind.REQUIRED, types(type, alsoAccepted), null);
    }

    public static Parameter optional(String name, Type type, Value defaultValue) {
        return new Parameter(name, Kind.OPTIONAL, List.of(type), defaultValue);
    }

    public static Parameter repeated(String name, Type type, Type... alsoAccepted) {
        return new Parameter(name, Kind.REPEATED, types(type, alsoAccepted), null);
    }

    /** Part of the query, of any type, which the function runs itself as often as it needs. */
    public static Parameter query(String name) {
        return new Parameter(name, Kind.QUERY, List.of(Type.values()), null);
    }

    /** The type that values this parameter doesn't accept are converted to. */
    public Type type() {
        return types.getFirst();
    }

    public boolean accepts(Type type) {
        return types.contains(type);
    }

    public boolean isOptional() {
        return kind == Kind.OPTIONAL;
    }

    public boolean isRepeated() {
        return kind == Kind.REPEATED;
    }

    public boolean isQuery() {
        return kind == Kind.QUERY;
    }

    private static List<Type> types(Type type, Type... alsoAccepted) {
        List<Type> types = new ArrayList<>(List.of(type));
        Collections.addAll(types, alsoAccepted);
        return types;
    }
}
