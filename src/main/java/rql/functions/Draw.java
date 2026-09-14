package rql.functions;

import java.util.ArrayList;
import java.util.List;
import java.util.random.RandomGenerator;

import rql.table.Row;
import rql.table.Table;
import rql.value.NumberValue;
import rql.value.TableValue;
import rql.value.Type;
import rql.value.Value;

/**
 * Rolls on a table {@code count} times without putting rows back, so no row comes up more than once. Useful
 * for things like choosing two different skills.
 */
public final class Draw implements Function {
    @Override
    public String name() {
        return "Draw";
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
            throw args.error(1, "can't draw a negative number of rows (" + count + ")");
        }

        // Chances are treated as weights, like Roll does, so rows with no chance can never be drawn.
        List<Row> remaining = new ArrayList<>(table.rows().stream().filter(row -> row.chance() > 0).toList());
        if (count > remaining.size()) {
            throw args.error(1, "can't draw " + count + " rows: " + table.name() + " only has "
                    + remaining.size() + (remaining.size() == 1 ? " row" : " rows") + " that can be rolled");
        }

        List<Row> drawn = new ArrayList<>(count);
        for (int i = 0; i < count; i++) {
            drawn.add(remaining.remove(drawIndex(remaining, random)));
        }
        return new TableValue(table.withRows(drawn));
    }

    private static int drawIndex(List<Row> rows, RandomGenerator random) {
        double totalChance = rows.stream().mapToDouble(Row::chance).sum();
        double target = random.nextDouble(totalChance);
        double cumulative = 0;
        for (int i = 0; i < rows.size(); i++) {
            cumulative += rows.get(i).chance();
            if (target < cumulative) {
                return i;
            }
        }
        // Rounding can leave the sum of chances just under the target.
        return rows.size() - 1;
    }
}
