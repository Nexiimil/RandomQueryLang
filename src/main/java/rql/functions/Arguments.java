package rql.functions;

import java.util.List;

import rql.QueryException;
import rql.table.Table;
import rql.value.NumberValue;
import rql.value.TableValue;
import rql.value.TextValue;
import rql.value.Value;

public final class Arguments {
    private final List<Value> values;
    private final List<Integer> positions;

    public Arguments(List<Value> values, List<Integer> positions) {
        this.values = List.copyOf(values);
        this.positions = List.copyOf(positions);
    }

    public Value value(int index) {
        return values.get(index);
    }

    public Table table(int index) {
        return ((TableValue) values.get(index)).table();
    }

    public int number(int index) {
        return ((NumberValue) values.get(index)).value();
    }

    public String text(int index) {
        return ((TextValue) values.get(index)).value();
    }

    public QueryException error(int index, String message) {
        return new QueryException(message, positions.get(index));
    }
}
