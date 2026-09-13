package rql.functions;

import java.util.List;
import java.util.random.RandomGenerator;

import rql.value.NumberValue;
import rql.value.Type;
import rql.value.Value;

/** Adds up the values of every row in a table. */
public final class Sum implements Builtin {
    @Override
    public String name() {
        return "Sum";
    }

    @Override
    public List<Param> params() {
        return List.of(Param.required("table", Type.TABLE));
    }

    @Override
    public Type returnType() {
        return Type.NUMBER;
    }

    @Override
    public Value call(Arguments args, RandomGenerator random) {
        long total = args.table(0).total();
        if (total < Integer.MIN_VALUE || total > Integer.MAX_VALUE) {
            throw args.error(0, "the total " + total + " is too large");
        }
        return new NumberValue((int) total);
    }
}
