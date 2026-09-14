package rql.functions;

import java.util.List;
import java.util.random.RandomGenerator;

import rql.value.TextValue;
import rql.value.Type;
import rql.value.Value;

/**
 * Joins text together. Text can name a table, so Draw(Concat(class, " Skills"), 2) draws from the skills
 * table for whichever class was rolled.
 */
public final class Concat implements Function {
    @Override
    public String name() {
        return "Concat";
    }

    @Override
    public List<Parameter> params() {
        return List.of(Parameter.required("text", Type.TEXT), Parameter.repeated("more", Type.TEXT));
    }

    @Override
    public Type returnType() {
        return Type.TEXT;
    }

    @Override
    public Value call(Arguments args, RandomGenerator random) {
        StringBuilder text = new StringBuilder();
        for (int i = 0; i < args.size(); i++) {
            text.append(args.text(i));
        }
        return new TextValue(text.toString());
    }
}
