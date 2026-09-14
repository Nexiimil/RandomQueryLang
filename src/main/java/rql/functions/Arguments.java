package rql.functions;

import java.util.ArrayList;
import java.util.List;

import rql.QueryException;
import rql.table.Table;
import rql.value.NumberValue;
import rql.value.TableValue;
import rql.value.TextValue;
import rql.value.Value;

/**
 * The arguments a function was called with, already converted to the types its parameters ask for,
 * with defaults filled in for any that were left out. Each one remembers where it was in the query,
 * so errors can point at it.
 */
public final class Arguments {
    // A Value for each argument, or a Query for arguments to query parameters.
    private final List<Object> arguments;
    // Where each argument starts in the query text, or the call's position for a filled-in default.
    private final List<Integer> positions;

    private Arguments(List<Object> arguments, List<Integer> positions) {
        this.arguments = List.copyOf(arguments);
        this.positions = List.copyOf(positions);
    }

    public static Builder builder() {
        return new Builder();
    }

    /** How many arguments there are, which is only worth asking for functions with a repeated parameter. */
    public int size() {
        return arguments.size();
    }

    /** The argument as it is, for parameters that accept more than one type, like Roll's table or dice. */
    public Value value(int index) {
        return (Value) arguments.get(index);
    }

    // These unwrap an argument whose parameter only accepts that type. They don't check the type,
    // because the interpreter has already converted the argument to it.

    public Table table(int index) {
        return ((TableValue) value(index)).table();
    }

    public int number(int index) {
        return ((NumberValue) value(index)).value();
    }

    public String text(int index) {
        return ((TextValue) value(index)).value();
    }

    /** The argument for a query parameter, which hasn't been run yet. */
    public Query query(int index) {
        return (Query) arguments.get(index);
    }

    /** An error to throw that points at this argument in the query. */
    public QueryException error(int index, String message) {
        return new QueryException(message, positions.get(index));
    }

    /** Collects arguments in order, for the interpreter to pass to a function. */
    public static final class Builder {
        private final List<Object> arguments = new ArrayList<>();
        private final List<Integer> positions = new ArrayList<>();

        private Builder() {
        }

        public Builder addValue(Value value, int position) {
            arguments.add(value);
            positions.add(position);
            return this;
        }

        public Builder addQuery(Query query, int position) {
            arguments.add(query);
            positions.add(position);
            return this;
        }

        public Arguments build() {
            return new Arguments(arguments, positions);
        }
    }
}
