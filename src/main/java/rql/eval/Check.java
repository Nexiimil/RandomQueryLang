package rql.eval;

import java.util.Map;

import rql.QueryException;
import rql.node.Context;
import rql.node.EvalException;
import rql.node.Node;
import rql.table.Table;

/**
 * Reports mistakes that would fail no matter what gets rolled, before anything is rolled.
 *
 * <p>It does this by running the parts of the tree that nothing random can reach. Those parts give the
 * same answer every time, so any error they would raise, they raise now — and the rules being checked
 * are the ones the nodes already have, rather than a second set written out again here and left to
 * drift.
 */
final class Check {
    private Check() {
    }

    static void beforeRolling(BoundQuery bound, Map<String, Table> tables) {
        check(bound.root(), Context.settled(tables), bound);
    }

    private static void check(Node<?> node, Context settled, BoundQuery bound) {
        if (isSettled(node)) {
            try {
                node.evaluate(settled);
            } catch (EvalException e) {
                throw new QueryException(e.getMessage(), bound.positionOf(e.blame()));
            }
            return;
        }
        for (Node<?> child : node.children()) {
            check(child, settled, bound);
        }
    }

    /** A node nothing random can reach, counting everything it is built from. */
    private static boolean isSettled(Node<?> node) {
        return !node.rolls() && node.children().stream().allMatch(Check::isSettled);
    }
}
