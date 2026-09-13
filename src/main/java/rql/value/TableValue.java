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
        if (table.things().isEmpty()) {
            return "(no rows)";
        }
        return table.things().stream()
                .map(thing -> thing.name() + " (" + thing.value() + ")")
                .collect(Collectors.joining(System.lineSeparator()));
    }
}
