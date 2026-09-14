package rql.functions;

import java.util.List;
import java.util.random.RandomGenerator;

import rql.value.TextValue;
import rql.value.Type;
import rql.value.Value;

/** The name of the row in a one-row table, as text. */
public final class Name implements Function {
    @Override
    public String name() {
        return "Name";
    }

    @Override
    public List<Parameter> params() {
        return List.of(Parameter.required("row", Type.TEXT));
    }

    @Override
    public Type returnType() {
        return Type.TEXT;
    }

    @Override
    public Value call(Arguments args, RandomGenerator random) {
        return new TextValue(args.text(0));
    }
}
