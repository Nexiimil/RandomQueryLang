package rql.node.function;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.random.RandomGenerator;
import java.util.stream.IntStream;

import rql.node.Context;
import rql.node.EvalException;
import rql.node.Node;
import rql.table.Table;
import rql.table.Thing;

/** What {@link DropLowest} and {@link DropHighest} share. */
final class Drops {
    private Drops() {
    }

    static Table drop(Node<Table> table, Node<Integer> count, boolean lowest, Context context) {
        int dropping = count.evaluate(context);
        if (dropping < 0) {
            throw new EvalException(count, "can't drop a negative number of rows (" + dropping + ")");
        }

        Table source = table.evaluate(context);
        List<Thing> things = source.things();
        List<Integer> order = new ArrayList<>(IntStream.range(0, things.size()).boxed().toList());
        // Shuffling before a stable sort means rows with the same value are dropped at random.
        Collections.shuffle(order, asRandom(context.random()));
        Comparator<Integer> byValue = Comparator.comparingInt(index -> things.get(index).value());
        order.sort(lowest ? byValue : byValue.reversed());

        Set<Integer> dropped = new HashSet<>(order.subList(0, Math.min(dropping, order.size())));
        List<Thing> kept = IntStream.range(0, things.size())
                .filter(index -> !dropped.contains(index))
                .mapToObj(things::get)
                .toList();
        return source.withThings(kept);
    }

    /** {@link Collections#shuffle} predates {@link RandomGenerator} and still wants a {@link java.util.Random}. */
    private static java.util.Random asRandom(RandomGenerator random) {
        return new java.util.Random() {
            @Override
            public int nextInt(int bound) {
                return random.nextInt(bound);
            }

            @Override
            public long nextLong() {
                return random.nextLong();
            }
        };
    }
}
