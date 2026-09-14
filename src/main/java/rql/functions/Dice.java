package rql.functions;

import java.util.List;
import java.util.random.RandomGenerator;

import rql.value.DiceValue;
import rql.value.Type;
import rql.value.Value;

/** Makes dice from numbers, like Dice(2, 6) for 2d6, so how many dice or how many sides can come from a roll. */
public final class Dice implements Function {
    @Override
    public String name() {
        return "Dice";
    }

    @Override
    public List<Parameter> params() {
        return List.of(Parameter.required("count", Type.NUMBER), Parameter.required("sides", Type.NUMBER));
    }

    @Override
    public Type returnType() {
        return Type.DICE;
    }

    @Override
    public Value call(Arguments args, RandomGenerator random) {
        int count = args.number(0);
        if (count < 1) {
            throw args.error(0, "can't roll " + count + " dice");
        }
        int sides = args.number(1);
        if (sides < 1) {
            throw args.error(1, "a die needs at least 1 side");
        }
        return new DiceValue(count, sides);
    }
}
