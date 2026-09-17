package rql.node;

import java.util.Map;
import java.util.random.RandomGenerator;

import rql.table.Table;

/** What a node needs while it runs: the tables in scope, and where random numbers come from. */
public record Context(Map<String, Table> tables, RandomGenerator random) {
    public Context {
        tables = Map.copyOf(tables);
    }

    /**
     * A context for evaluating a part of a tree that nothing random can reach, so its result is the
     * same every time and can be worked out before the query is run. Asking for a random number here
     * is a mistake in whatever decided the subtree was settled, so it fails loudly rather than
     * quietly rolling early.
     */
    public static Context settled(Map<String, Table> tables) {
        return new Context(tables, new RandomGenerator() {
            @Override
            public long nextLong() {
                throw new IllegalStateException("a subtree treated as settled tried to roll");
            }
        });
    }
}
