package rql.node;

import java.util.ArrayList;
import java.util.List;
import java.util.function.BiFunction;
import java.util.function.Function;
import java.util.stream.Collectors;

/**
 * How a function is written in a query, and how to build its node from what was written.
 *
 * <p>Built through a chain that remembers the type of each parameter as it goes, so the constructor
 * handed to {@code returns} is checked against them when this file is compiled:
 *
 * <pre>
 * Signature.named("Roll")
 *         .arg("table", ValueType.TABLE)
 *         .optional("count", ValueType.NUMBER, 1)
 *         .returns(ValueType.TABLE, RollTable::new);
 * </pre>
 *
 * A parameter list that doesn't match the node's components is a compile error rather than something
 * that goes wrong once a query uses it.
 */
public final class Signature<R> {
    private final String name;
    private final List<Param<?>> params;
    private final ValueType<R> returns;
    private final Function<List<Node<?>>, Node<R>> factory;

    private Signature(String name, List<Param<?>> params, ValueType<R> returns,
            Function<List<Node<?>>, Node<R>> factory) {
        this.name = name;
        this.params = List.copyOf(params);
        this.returns = returns;
        this.factory = factory;
    }

    public String name() {
        return name;
    }

    public List<Param<?>> params() {
        return params;
    }

    public ValueType<R> returns() {
        return returns;
    }

    /** How many arguments a query has to give, the rest having defaults. */
    public long required() {
        return params.stream().filter(param -> !param.isOptional()).count();
    }

    /** How this function is written, for an error that says what was expected. */
    public String usage() {
        return name + params.stream().map(Param::toString).collect(Collectors.joining(", ", "(", ")"));
    }

    /**
     * Builds the node, filling in the defaults of any parameters the query left out. Arguments must
     * already be the right types; the binder is what reports a query that got them wrong, so anything
     * still wrong here is a mistake in the binder rather than in the query.
     */
    public Node<R> build(List<Node<?>> arguments) {
        if (arguments.size() < required() || arguments.size() > params.size()) {
            throw new IllegalArgumentException(
                    "expected " + usage() + " but got " + arguments.size() + " arguments");
        }

        List<Node<?>> complete = new ArrayList<>(arguments);
        for (int i = arguments.size(); i < params.size(); i++) {
            complete.add(params.get(i).defaultNode());
        }
        for (int i = 0; i < complete.size(); i++) {
            ValueType<?> wanted = params.get(i).type();
            ValueType<?> given = complete.get(i).type();
            if (given != wanted) {
                throw new IllegalArgumentException(name + " wanted " + wanted.description() + " for "
                        + params.get(i).name() + " but was given " + given.description());
            }
        }
        return factory.apply(complete);
    }

    public static Builder0 named(String name) {
        return new Builder0(name);
    }

    /**
     * The one cast in the design. It is safe because {@link #build} has just checked every argument's
     * {@link Node#type()} against the parameter that {@code A} came from.
     */
    @SuppressWarnings("unchecked")
    private static <A> Node<A> at(List<Node<?>> arguments, int index) {
        return (Node<A>) arguments.get(index);
    }

    private static List<Param<?>> and(List<Param<?>> params, Param<?> next) {
        List<Param<?>> grown = new ArrayList<>(params);
        grown.add(next);
        return grown;
    }

    /** A function with no parameters chosen yet. */
    public static final class Builder0 {
        private final String name;

        private Builder0(String name) {
            this.name = name;
        }

        public <A> Builder1<A> arg(String param, ValueType<A> type) {
            return new Builder1<>(name, List.of(Param.required(param, type)));
        }

        public <A> Builder1<A> optional(String param, ValueType<A> type, A defaultValue) {
            return new Builder1<>(name, List.of(Param.optional(param, type, defaultValue)));
        }
    }

    /** A function with one parameter, of type {@code A}. */
    public static final class Builder1<A> {
        private final String name;
        private final List<Param<?>> params;

        private Builder1(String name, List<Param<?>> params) {
            this.name = name;
            this.params = params;
        }

        public <B> Builder2<A, B> arg(String param, ValueType<B> type) {
            return new Builder2<>(name, and(params, Param.required(param, type)));
        }

        public <B> Builder2<A, B> optional(String param, ValueType<B> type, B defaultValue) {
            return new Builder2<>(name, and(params, Param.optional(param, type, defaultValue)));
        }

        public <R> Signature<R> returns(ValueType<R> type, Function<Node<A>, Node<R>> node) {
            return new Signature<>(name, params, type, arguments -> node.apply(at(arguments, 0)));
        }
    }

    /** A function with two parameters, of types {@code A} and {@code B}. */
    public static final class Builder2<A, B> {
        private final String name;
        private final List<Param<?>> params;

        private Builder2(String name, List<Param<?>> params) {
            this.name = name;
            this.params = params;
        }

        public <C> Builder3<A, B, C> arg(String param, ValueType<C> type) {
            return new Builder3<>(name, and(params, Param.required(param, type)));
        }

        public <C> Builder3<A, B, C> optional(String param, ValueType<C> type, C defaultValue) {
            return new Builder3<>(name, and(params, Param.optional(param, type, defaultValue)));
        }

        public <R> Signature<R> returns(ValueType<R> type, BiFunction<Node<A>, Node<B>, Node<R>> node) {
            return new Signature<>(name, params, type,
                    arguments -> node.apply(at(arguments, 0), at(arguments, 1)));
        }
    }

    /** A function with three parameters, of types {@code A}, {@code B} and {@code C}. */
    public static final class Builder3<A, B, C> {
        private final String name;
        private final List<Param<?>> params;

        private Builder3(String name, List<Param<?>> params) {
            this.name = name;
            this.params = params;
        }

        public <R> Signature<R> returns(ValueType<R> type, ThreeArgs<A, B, C, R> node) {
            return new Signature<>(name, params, type,
                    arguments -> node.apply(at(arguments, 0), at(arguments, 1), at(arguments, 2)));
        }
    }

    /** What {@code BiFunction} would be with one more argument. */
    @FunctionalInterface
    public interface ThreeArgs<A, B, C, R> {
        Node<R> apply(Node<A> first, Node<B> second, Node<C> third);
    }
}
