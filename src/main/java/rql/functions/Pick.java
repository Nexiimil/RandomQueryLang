package rql.functions;

import java.util.List;
import java.util.random.RandomGenerator;

import rql.table.Table;
import rql.value.TableValue;
import rql.value.Type;
import rql.value.Value;

/** Picks a row by counting down from the top of a table, so row 1 is the first row. */
public final class Pick implements Builtin {
    @Override
    public String name() {
        return "Pick";
    }

    @Override
    public List<Param> params() {
        return List.of(Param.required("table", Type.TABLE), Param.required("row", Type.NUMBER));
    }

    @Override
    public Type returnType() {
        return Type.TABLE;
    }

    @Override
    public Value call(Arguments args, RandomGenerator random) {
        Table table = args.table(0);
        int row = args.number(1);
        if (row < 1 || row > table.size()) {
            String rows = table.size() == 0 ? "has no rows" : "has rows 1 to " + table.size();
            throw args.error(1, "can't pick row " + row + ": " + table.name() + " " + rows);
        }
        return new TableValue(table.withThings(List.of(table.things().get(row - 1))));
    }
}
