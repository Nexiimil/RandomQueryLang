package rql.eval;

import java.util.Map;
import java.util.Optional;
import java.util.random.RandomGenerator;

import rql.table.Table;

public final class Environment {
    private final Map<String, Table> tables;
    private final RandomGenerator random;

    public Environment(Map<String, Table> tables, RandomGenerator random) {
        this.tables = Map.copyOf(tables);
        this.random = random;
    }

    public Optional<Table> findTable(String name) {
        return Optional.ofNullable(tables.get(name));
    }

    public RandomGenerator random() {
        return random;
    }
}
