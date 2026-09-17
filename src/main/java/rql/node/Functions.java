package rql.node;

import java.util.List;

import rql.node.function.DropHighest;
import rql.node.function.DropLowest;
import rql.node.function.Pick;
import rql.node.function.RollDice;
import rql.node.function.RollTable;
import rql.node.function.Sum;

/**
 * Every function a query can call. Two signatures can share a name, as Roll does: the binder picks
 * between them by the type of the first argument, which is how rolling dice and rolling on a table
 * stay separate nodes.
 */
public final class Functions {
    private Functions() {
    }

    public static List<Signature<?>> all() {
        return List.of(
                RollTable.SIGNATURE,
                RollDice.SIGNATURE,
                Pick.SIGNATURE,
                DropLowest.SIGNATURE,
                DropHighest.SIGNATURE,
                Sum.SIGNATURE);
    }
}
