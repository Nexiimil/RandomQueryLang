package rql.functions;

import java.util.List;
import java.util.random.RandomGenerator;

import rql.value.NumberValue;
import rql.value.Type;
import rql.value.Value;

/** Takes one number away from another. */
public final class Subtract implements Function {
    @Override
    public String name() {
        return "Subtract";
    }

    @Override
    public List<Parameter> params() {
        return List.of(Parameter.required("number", Type.NUMBER), Parameter.required("amount", Type.NUMBER));
    }

    @Override
    public Type returnType() {
        return Type.NUMBER;
    }

    @Override
    public Value call(Arguments args, RandomGenerator random) {
        long result = (long) args.number(0) - args.number(1);
        if (result < Integer.MIN_VALUE || result > Integer.MAX_VALUE) {
            throw args.error(0, "the result " + result + " is too large");
        }
        return new NumberValue((int) result);
    }
}
