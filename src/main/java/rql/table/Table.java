package rql.table;

import java.util.List;

public record Table(String name, List<Row> rows) {
    public Table {
        rows = List.copyOf(rows);
    }

    public int size() {
        return rows.size();
    }

    public long total() {
        return rows.stream().mapToLong(Row::value).sum();
    }

    public Table withRows(List<Row> newRows) {
        return new Table(name, newRows);
    }
}
