package rql.value;

import java.util.stream.Collectors;

import rql.table.Table;

public record TableValue(Table table) implements Value {
    @Override
    public Type type() {
        return Type.TABLE;
    }

    @Override
    public String display() {
        if (table.size() == 0) {
            return "(no rows)";
        }
        return table.rows().stream()
                .map(row -> row.name() + " (" + row.value() + ")")
                .collect(Collectors.joining(System.lineSeparator()));
    }
}
