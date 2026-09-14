package rql.functions;

import java.util.ArrayList;
import java.util.List;
import java.util.random.RandomGenerator;

import rql.table.Row;
import rql.table.Table;
import rql.value.TableValue;
import rql.value.Type;
import rql.value.Value;

/** Puts the rows of several tables together into one table, in the order they were given. */
public final class Join implements Function {
    @Override
    public String name() {
        return "Join";
    }

    @Override
    public List<Parameter> params() {
        return List.of(Parameter.required("table", Type.TABLE), Parameter.repeated("more", Type.TABLE));
    }

    @Override
    public Type returnType() {
        return Type.TABLE;
    }

    @Override
    public Value call(Arguments args, RandomGenerator random) {
        List<String> names = new ArrayList<>();
        List<Row> rows = new ArrayList<>();
        for (int i = 0; i < args.size(); i++) {
            Table table = args.table(i);
            names.add(table.name());
            rows.addAll(table.rows());
        }
        return new TableValue(new Table(String.join(" + ", names), rows));
    }
}
