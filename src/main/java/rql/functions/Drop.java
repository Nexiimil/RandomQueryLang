package rql.functions;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.random.RandomGenerator;
import java.util.stream.IntStream;

import rql.table.Table;
import rql.table.Thing;
import rql.value.NumberValue;
import rql.value.TableValue;
import rql.value.Type;
import rql.value.Value;

/** DropLowest and DropHighest: remove the {@code count} lowest or highest valued rows from a table. */
public final class Drop implements Builtin {
    private final String name;
    private final boolean lowest;

    private Drop(String name, boolean lowest) {
        this.name = name;
        this.lowest = lowest;
    }

    public static Drop lowest() {
        return new Drop("DropLowest", true);
    }

    public static Drop highest() {
        return new Drop("DropHighest", false);
    }

    @Override
    public String name() {
        return name;
    }

    @Override
    public List<Param> params() {
        return List.of(
                Param.required("table", Type.TABLE),
                Param.optional("count", Type.NUMBER, new NumberValue(1)));
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
            throw args.error(1, "can't drop a negative number of rows (" + count + ")");
        }

        List<Thing> things = table.things();
        List<Integer> order = new ArrayList<>(IntStream.range(0, things.size()).boxed().toList());
        // Shuffling before a stable sort means rows with the same value are dropped at random.
        Collections.shuffle(order, random);
        Comparator<Integer> byValue = Comparator.comparingInt(index -> things.get(index).value());
        order.sort(lowest ? byValue : byValue.reversed());

        Set<Integer> dropped = new HashSet<>(order.subList(0, Math.min(count, order.size())));
        List<Thing> kept = IntStream.range(0, things.size())
                .filter(index -> !dropped.contains(index))
                .mapToObj(things::get)
                .toList();
        return new TableValue(table.withThings(kept));
    }
}
