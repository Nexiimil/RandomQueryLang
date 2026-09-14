package rql.functions;

import java.util.ArrayList;
import java.util.List;
import java.util.random.RandomGenerator;

import rql.table.Row;
import rql.table.Table;
import rql.value.DiceValue;
import rql.value.NumberValue;
import rql.value.TableValue;
import rql.value.Type;
import rql.value.Value;

/**
 * Rolls on a table, or rolls dice, {@code count} times. Every roll is separate, so the same row can come
 * up more than once.
 */
public final class Roll implements Function {
    @Override
    public String name() {
        return "Roll";
    }

    @Override
    public List<Parameter> params() {
        return List.of(
                Parameter.required("table", Type.TABLE, Type.DICE),
                Parameter.optional("count", Type.NUMBER, new NumberValue(1)));
    }

    @Override
    public Type returnType() {
        return Type.TABLE;
    }

    @Override
    public Value call(Arguments args, RandomGenerator random) {
        int count = args.number(1);
        if (count < 0) {
            throw args.error(1, "can't roll a negative number of times (" + count + ")");
        }
        if (args.value(0) instanceof DiceValue dice) {
            return rollDice(dice, count, random);
        }

        Table table = args.table(0);
        if (count == 0) {
            return new TableValue(table.withRows(List.of()));
        }

        // Chances are treated as weights, so a table with rows dropped from it still rolls fairly.
        double totalChance = table.rows().stream().mapToDouble(Row::chance).sum();
        if (totalChance <= 0) {
            throw args.error(0, table.name() + " has no rows that can be rolled");
        }

        List<Row> rolled = new ArrayList<>(count);
        for (int i = 0; i < count; i++) {
            rolled.add(rollOnce(table, totalChance, random));
        }
        return new TableValue(table.withRows(rolled));
    }

    private static Value rollDice(DiceValue dice, int count, RandomGenerator random) {
        List<Row> faces = new ArrayList<>();
        for (int i = 0; i < count; i++) {
            faces.addAll(dice.roll(random).rows());
        }
        return new TableValue(new Table(dice.display(), faces));
    }

    private static Row rollOnce(Table table, double totalChance, RandomGenerator random) {
        double target = random.nextDouble(totalChance);
        double cumulative = 0;
        Row lastRollable = null;
        for (Row row : table.rows()) {
            if (row.chance() > 0) {
                cumulative += row.chance();
                lastRollable = row;
                if (target < cumulative) {
                    return row;
                }
            }
        }
        // Rounding can leave the sum of chances just under the target.
        return lastRollable;
    }
}
