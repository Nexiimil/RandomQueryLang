package rql.functions;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.random.RandomGenerator;
import java.util.stream.IntStream;

import rql.table.Row;
import rql.table.Table;
import rql.value.NumberValue;
import rql.value.TableValue;
import rql.value.Type;
import rql.value.Value;

/** Keeps the {@code count} highest valued rows of a table and removes the rest. Ties are broken at random. */
public final class KeepHighest implements Function {
    @Override
    public String name() {
        return "KeepHighest";
    }

    @Override
    public List<Parameter> params() {
        return List.of(
                Parameter.required("table", Type.TABLE),
                Parameter.optional("count", Type.NUMBER, new NumberValue(1)));
    }

    @Override
    public Type returnType() {
        return Type.TABLE;
    }

    @Override
    public Value call(Arguments args, RandomGenerator random) {
        Table table = args.table(0);
        int count = args.number(1);
        if (count < 0) {
            throw args.error(1, "can't keep a negative number of rows (" + count + ")");
        }

        List<Row> rows = table.rows();
        List<Integer> order = new ArrayList<>(IntStream.range(0, table.size()).boxed().toList());
        // Shuffling before a stable sort means rows with the same value are kept at random.
        Collections.shuffle(order, random);
        Comparator<Integer> byValue = Comparator.comparingInt(index -> rows.get(index).value());
        order.sort(byValue.reversed());

        Set<Integer> keep = new HashSet<>(order.subList(0, Math.min(count, order.size())));
        List<Row> kept = IntStream.range(0, table.size())
                .filter(index -> keep.contains(index))
                .mapToObj(rows::get)
                .toList();
        return new TableValue(table.withRows(kept));
    }
}
