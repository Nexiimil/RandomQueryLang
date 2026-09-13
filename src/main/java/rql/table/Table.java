package rql.table;

import java.util.List;

public record Table(String name, List<Thing> things) {
    public Table {
        things = List.copyOf(things);
    }

    public int size() {
        return things.size();
    }

    public long total() {
        return things.stream().mapToLong(Thing::value).sum();
    }

    public Table withThings(List<Thing> newThings) {
        return new Table(name, newThings);
    }
}
