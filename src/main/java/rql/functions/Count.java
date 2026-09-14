package rql.functions;

import java.util.List;
import java.util.random.RandomGenerator;

import rql.value.NumberValue;
import rql.value.Type;
import rql.value.Value;

/** Counts the rows in a table. */
public final class Count implements Function {
    @Override
    public String name() {
        return "Count";
    }

    @Override
    public List<Parameter> params() {
        return List.of(Parameter.required("table", Type.TABLE));
    }

    @Override
    public Type returnType() {
        return Type.NUMBER;
    }

    @Override
    public Value call(Arguments args, RandomGenerator random) {
        return new NumberValue(args.table(0).size());
    }
}
