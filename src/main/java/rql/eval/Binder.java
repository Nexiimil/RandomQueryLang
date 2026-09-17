package rql.eval;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.IdentityHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

import rql.QueryException;
import rql.node.Coerce;
import rql.node.Dice;
import rql.node.Functions;
import rql.node.Literal;
import rql.node.Node;
import rql.node.Param;
import rql.node.Signature;
import rql.node.ValueType;
import rql.syntax.Expr;
import rql.table.Table;

/**
 * Turns what was written into a tree. Everything that can be settled without rolling is settled here:
 * which function was meant, whether it was given the right number of arguments, which tables the names
 * refer to, and what has to be converted to what.
 *
 * <p>A tree that comes out of here has types that already line up, which is why nothing downstream
 * checks them again.
 */
public final class Binder {
    private final Map<String, Table> tables;
    private final Map<String, List<Signature<?>>> byName = new HashMap<>();
    private final Map<Node<?>, Integer> positions = new IdentityHashMap<>();

    private Binder(Map<String, Table> tables) {
        this.tables = tables;
        for (Signature<?> signature : Functions.all()) {
            byName.computeIfAbsent(key(signature.name()), name -> new ArrayList<>()).add(signature);
        }
    }

    public static BoundQuery bind(Expr expr, Map<String, Table> tables) {
        Binder binder = new Binder(tables);
        Node<?> root = binder.node(expr);
        return new BoundQuery(root, binder.positions);
    }

    private Node<?> node(Expr expr) {
        Node<?> bound = switch (expr) {
            case Expr.NumberLiteral number -> Literal.of(number.value());
            case Expr.TextLiteral text -> Literal.of(text.value());
            case Expr.DiceLiteral dice -> Literal.of(new Dice(dice.count(), dice.sides()));
            case Expr.Identifier identifier -> Literal.of(table(identifier));
            case Expr.Call call -> call(call);
        };
        positions.putIfAbsent(bound, expr.position());
        return bound;
    }

    private Node<?> call(Expr.Call call) {
        List<Signature<?>> candidates = byName.get(key(call.name()));
        if (candidates == null) {
            throw new QueryException("unknown function " + call.name(), call.position());
        }

        List<Node<?>> arguments = new ArrayList<>();
        for (Expr argument : call.arguments()) {
            arguments.add(node(argument));
        }

        Signature<?> signature = choose(candidates, arguments);
        if (arguments.size() < signature.required() || arguments.size() > signature.params().size()) {
            throw new QueryException("expected " + signature.usage() + " but got " + arguments.size()
                    + (arguments.size() == 1 ? " argument" : " arguments"), call.position());
        }

        List<Node<?>> converted = new ArrayList<>(arguments.size());
        for (int i = 0; i < arguments.size(); i++) {
            converted.add(convert(signature.params().get(i), arguments.get(i), call.arguments().get(i)));
        }
        return signature.build(converted);
    }

    /**
     * Picks between functions sharing a name by the type of the first argument, which is what keeps
     * rolling dice and rolling on a table separate nodes. Anything that matches none of them falls to
     * the first, so that it gets converted and reported against the usual shape of the function.
     */
    private static Signature<?> choose(List<Signature<?>> candidates, List<Node<?>> arguments) {
        if (candidates.size() == 1 || arguments.isEmpty()) {
            return candidates.getFirst();
        }
        ValueType<?> given = arguments.getFirst().type();
        return candidates.stream()
                .filter(candidate -> !candidate.params().isEmpty())
                .filter(candidate -> candidate.params().getFirst().type() == given)
                .findFirst()
                .orElse(candidates.getFirst());
    }

    private Node<?> convert(Param<?> param, Node<?> argument, Expr wrote) {
        if (argument.type() == param.type()) {
            return argument;
        }
        if (!Coerce.possible(argument.type(), param.type())) {
            throw new QueryException(
                    "expected " + param.type().description() + " but " + describe(argument, wrote),
                    wrote.position());
        }
        Node<?> coerced = Coerce.to(param.type(), argument);
        positions.putIfAbsent(coerced, wrote.position());
        return coerced;
    }

    private static String describe(Node<?> argument, Expr wrote) {
        if (wrote instanceof Expr.Call call) {
            return call.name() + " gives " + argument.type().description();
        }
        if (wrote instanceof Expr.NumberLiteral number) {
            return "got the number " + number.value();
        }
        return "got " + argument.type().description();
    }

    private Table table(Expr.Identifier identifier) {
        Table table = tables.get(identifier.name());
        if (table == null) {
            throw new QueryException("no table named \"" + identifier.name() + "\"", identifier.position());
        }
        return table;
    }

    private static String key(String functionName) {
        return functionName.toLowerCase(Locale.ROOT);
    }
}
