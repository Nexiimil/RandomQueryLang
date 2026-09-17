package rql.node.function;

import java.util.List;

import rql.node.Context;
import rql.node.EvalException;
import rql.node.Node;
import rql.node.Signature;
import rql.node.ValueType;
import rql.table.Table;

/** Adds up the values of every row in a table. */
public record Sum(Node<Table> table) implements Node<Integer> {
    public static final Signature<Integer> SIGNATURE = Signature.named("Sum")
            .arg("table", ValueType.TABLE)
            .returns(ValueType.NUMBER, Sum::new);

    @Override
    public ValueType<Integer> type() {
        return ValueType.NUMBER;
    }

    @Override
    public List<Node<?>> children() {
        return List.of(table);
    }

    @Override
    public Integer evaluate(Context context) {
        long total = table.evaluate(context).total();
        if (total < Integer.MIN_VALUE || total > Integer.MAX_VALUE) {
            throw new EvalException(table, "the total " + total + " is too large");
        }
        return (int) total;
    }
}
