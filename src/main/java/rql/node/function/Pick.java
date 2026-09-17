package rql.node.function;

import java.util.List;

import rql.node.Context;
import rql.node.EvalException;
import rql.node.Node;
import rql.node.Signature;
import rql.node.ValueType;
import rql.table.Table;

/** Picks a row by counting down from the top of a table, so row 1 is the first row. */
public record Pick(Node<Table> table, Node<Integer> row) implements Node<Table> {
    public static final Signature<Table> SIGNATURE = Signature.named("Pick")
            .arg("table", ValueType.TABLE)
            .arg("row", ValueType.NUMBER)
            .returns(ValueType.TABLE, Pick::new);

    @Override
    public ValueType<Table> type() {
        return ValueType.TABLE;
    }

    @Override
    public List<Node<?>> children() {
        return List.of(table, row);
    }

    @Override
    public Table evaluate(Context context) {
        Table source = table.evaluate(context);
        int wanted = row.evaluate(context);
        if (wanted < 1 || wanted > source.size()) {
            String rows = source.size() == 0 ? "has no rows" : "has rows 1 to " + source.size();
            throw new EvalException(row, "can't pick row " + wanted + ": " + source.name() + " " + rows);
        }
        return source.withThings(List.of(source.things().get(wanted - 1)));
    }
}
