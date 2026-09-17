package rql.node.function;

import java.util.ArrayList;
import java.util.List;

import rql.node.Context;
import rql.node.Dice;
import rql.node.EvalException;
import rql.node.Node;
import rql.node.Signature;
import rql.node.ValueType;
import rql.table.Table;
import rql.table.Thing;

/**
 * Rolls dice {@code count} times, giving a row for every die. {@code Roll(4d6, 2)} gives 8 rows, which
 * is why dice and tables are rolled by separate nodes rather than one that checks what it was given.
 */
public record RollDice(Node<Dice> dice, Node<Integer> count) implements Node<Table> {
    public static final Signature<Table> SIGNATURE = Signature.named("Roll")
            .arg("dice", ValueType.DICE)
            .optional("count", ValueType.NUMBER, 1)
            .returns(ValueType.TABLE, RollDice::new);

    @Override
    public ValueType<Table> type() {
        return ValueType.TABLE;
    }

    @Override
    public List<Node<?>> children() {
        return List.of(dice, count);
    }

    @Override
    public boolean rolls() {
        return true;
    }

    @Override
    public Table evaluate(Context context) {
        int times = count.evaluate(context);
        if (times < 0) {
            throw new EvalException(count, "can't roll a negative number of times (" + times + ")");
        }

        Dice rolling = dice.evaluate(context);
        List<Thing> faces = new ArrayList<>(times * rolling.count());
        for (int i = 0; i < times; i++) {
            faces.addAll(rolling.roll(context.random()).things());
        }
        return new Table(rolling.display(), faces);
    }
}
