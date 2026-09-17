package rql.node.function;

import java.util.ArrayList;
import java.util.List;
import java.util.random.RandomGenerator;

import rql.node.Context;
import rql.node.EvalException;
import rql.node.Node;
import rql.node.Signature;
import rql.node.ValueType;
import rql.table.Table;
import rql.table.Thing;

/**
 * Rolls on a table {@code count} times. Every roll is separate, so the same row can come up more than
 * once.
 */
public record RollTable(Node<Table> table, Node<Integer> count) implements Node<Table> {
    public static final Signature<Table> SIGNATURE = Signature.named("Roll")
            .arg("table", ValueType.TABLE)
            .optional("count", ValueType.NUMBER, 1)
            .returns(ValueType.TABLE, RollTable::new);

    @Override
    public ValueType<Table> type() {
        return ValueType.TABLE;
    }

    @Override
    public List<Node<?>> children() {
        return List.of(table, count);
    }

    @Override
    public boolean rolls() {
        return true;
    }

    @Override
    public Table evaluate(Context context) {
        int times = count.evaluate(context);
        if (times < 0) {
            throw new EvalException(count, "can't roll a negative number of times (" + times + ")");
        }

        Table source = table.evaluate(context);
        if (times == 0) {
            return source.withThings(List.of());
        }

        // Chances are treated as weights, so a table with rows dropped from it still rolls fairly.
        double totalChance = source.things().stream().mapToDouble(Thing::chance).sum();
        if (totalChance <= 0) {
            throw new EvalException(table, source.name() + " has no rows that can be rolled");
        }

        List<Thing> rolled = new ArrayList<>(times);
        for (int i = 0; i < times; i++) {
            rolled.add(rollOnce(source, totalChance, context.random()));
        }
        return source.withThings(rolled);
    }

    private static Thing rollOnce(Table table, double totalChance, RandomGenerator random) {
        double target = random.nextDouble(totalChance);
        double cumulative = 0;
        Thing lastRollable = null;
        for (Thing thing : table.things()) {
            if (thing.chance() > 0) {
                cumulative += thing.chance();
                lastRollable = thing;
                if (target < cumulative) {
                    return thing;
                }
            }
        }
        // Rounding can leave the sum of chances just under the target.
        return lastRollable;
    }
}
