package rql.functions;

import java.util.List;
import java.util.random.RandomGenerator;
import java.util.stream.Collectors;

import rql.value.Type;
import rql.value.Value;

public interface Builtin {
    String name();

    List<Param> params();

    Type returnType();

    /**
     * Runs the function. Arguments have already been converted to the types in {@link #params()},
     * with defaults filled in for optional arguments that were left out.
     */
    Value call(Arguments args, RandomGenerator random);

    default String usage() {
        return name() + params().stream()
                .map(param -> param.isOptional() ? "[" + param.name() + "]" : param.name())
                .collect(Collectors.joining(", ", "(", ")"));
    }
}
