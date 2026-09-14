package rql.functions;

import java.util.ArrayList;
import java.util.List;
import java.util.random.RandomGenerator;

import rql.table.Row;
import rql.table.Table;
import rql.value.DiceValue;
import rql.value.NumberValue;
import rql.value.TableValue;
import rql.value.TextValue;
import rql.value.Type;
import rql.value.Value;

/**
 * Runs a query {@code count} times, rolling again each time, and puts every result into one table. So
 * Repeat(Sum(DropLowest(4d6)), 6) gives six separate ability scores.
 */
public final class Repeat implements Function {
    @Override
    public String name() {
        return "Repeat";
    }

    @Override
    public List<Parameter> params() {
        return List.of(Parameter.query("query"), Parameter.required("count", Type.NUMBER));
    }

    @Override
    public Type returnType() {
        return Type.TABLE;
    }

    @Override
    public Value call(Arguments args, RandomGenerator random) {
        int count = args.number(1);
        if (count < 0) {
            throw args.error(1, "can't repeat a negative number of times (" + count + ")");
        }

        List<Row> rows = new ArrayList<>();
        for (int i = 0; i < count; i++) {
            addRows(rows, args.query(0).run(), random);
        }
        return new TableValue(new Table("Repeat", rows));
    }

    // Tables and dice add all of their rows. Numbers and text each become a row, valued the same way a row
    // in a table file would be.
    private static void addRows(List<Row> rows, Value result, RandomGenerator random) {
        switch (result) {
            case TableValue table -> rows.addAll(table.table().rows());
            case DiceValue dice -> rows.addAll(dice.roll(random).rows());
            case NumberValue number -> rows.add(new Row(number.display(), number.value(), 1));
            case TextValue text -> rows.add(
                    new Row(text.value(), Row.defaultValue(text.value(), rows.size() + 1), 1));
        }
    }
}
