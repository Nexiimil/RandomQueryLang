package rql.functions;

import java.util.ArrayList;
import java.util.List;
import java.util.random.RandomGenerator;

import rql.table.Table;
import rql.table.Thing;
import rql.value.DiceValue;
import rql.value.NumberValue;
import rql.value.TableValue;
import rql.value.Type;
import rql.value.Value;

/**
 * Rolls on a table, or rolls dice, {@code count} times. Every roll is separate, so the same row can come
 * up more than once.
 */
public final class Roll implements Builtin {
    @Override
    public String name() {
        return "Roll";
    }

    @Override
    public List<Param> params() {
        return List.of(
                Param.required("table", Type.TABLE, Type.DICE),
                Param.optional("count", Type.NUMBER, new NumberValue(1)));
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
            return new TableValue(table.withThings(List.of()));
        }

        // Chances are treated as weights, so a table with rows dropped from it still rolls fairly.
        double totalChance = table.things().stream().mapToDouble(Thing::chance).sum();
        if (totalChance <= 0) {
            throw args.error(0, table.name() + " has no rows that can be rolled");
        }

        List<Thing> rolled = new ArrayList<>(count);
        for (int i = 0; i < count; i++) {
            rolled.add(rollOnce(table, totalChance, random));
        }
        return new TableValue(table.withThings(rolled));
    }

    private static Value rollDice(DiceValue dice, int count, RandomGenerator random) {
        List<Thing> faces = new ArrayList<>();
        for (int i = 0; i < count; i++) {
            faces.addAll(dice.roll(random).things());
        }
        return new TableValue(new Table(dice.display(), faces));
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
