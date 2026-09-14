package rql.functions;

import java.util.List;

public final class Functions {
    private Functions() {
    }

    public static List<Function> all() {
        return List.of(
                new Roll(), new Draw(), new Pick(),
                new DropLowest(), new DropHighest(), new KeepLowest(), new KeepHighest(),
                new Sum(), new Count(), new Add(), new Subtract(), new Multiply(), new Divide(),
                new Dice(), new Repeat(), new Name(), new Concat(), new Label(), new Join());
    }
}
