package rql.functions;

import java.util.List;
import java.util.random.RandomGenerator;

import rql.table.Table;
import rql.value.TableValue;
import rql.value.Type;
import rql.value.Value;

/** Picks a row by counting down from the top of a table, so row 1 is the first row. */
public final class Pick implements Function {
    @Override
    public String name() {
        return "Pick";
    }

    @Override
    public List<Parameter> params() {
        return List.of(Parameter.required("table", Type.TABLE), Parameter.required("row", Type.NUMBER));
    }

    @Override
    public Type returnType() {
        return Type.TABLE;
    }

    @Override
    public Value call(Arguments args, RandomGenerator random) {
        Table table = args.table(0);
        int targetRow = args.number(1);
        if (targetRow < 1 || targetRow > table.size()) {
            String rows = table.size() == 0 ? "has no rows" : "has rows 1 to " + table.size();
            throw args.error(1, "can't pick row " + targetRow + ": " + table.name() + " " + rows);
        }
        return new TableValue(table.withRows(List.of(table.rows().get(targetRow - 1))));
    }
}
