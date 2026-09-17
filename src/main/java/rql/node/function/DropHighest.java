package rql.node.function;

import java.util.List;

import rql.node.Context;
import rql.node.Node;
import rql.node.Signature;
import rql.node.ValueType;
import rql.table.Table;

/**
 * Removes the {@code count} highest valued rows from a table. Where more rows tie for highest than are
 * being dropped, which of them goes is decided at random.
 */
public record DropHighest(Node<Table> table, Node<Integer> count) implements Node<Table> {
    public static final Signature<Table> SIGNATURE = Signature.named("DropHighest")
            .arg("table", ValueType.TABLE)
            .optional("count", ValueType.NUMBER, 1)
            .returns(ValueType.TABLE, DropHighest::new);

    @Override
    public ValueType<Table> type() {
        return ValueType.TABLE;
    }

    @Override
    public List<Node<?>> children() {
        return List.of(table, count);
    }

    /** Ties between equal rows are broken at random, so even a settled table can't be dropped early. */
    @Override
    public boolean rolls() {
        return true;
    }

    @Override
    public Table evaluate(Context context) {
        return Drops.drop(table, count, false, context);
    }
}
