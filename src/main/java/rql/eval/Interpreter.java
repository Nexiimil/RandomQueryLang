package rql.eval;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

import rql.QueryException;
import rql.functions.Arguments;
import rql.functions.Builtin;
import rql.functions.Builtins;
import rql.functions.Param;
import rql.syntax.Expr;
import rql.syntax.Parser;
import rql.value.DiceValue;
import rql.value.NumberValue;
import rql.value.TableValue;
import rql.value.TextValue;
import rql.value.Type;
import rql.value.Value;

public final class Interpreter {
    private final Environment environment;
    private final Map<String, Builtin> builtins = new HashMap<>();

    public Interpreter(Environment environment) {
        this.environment = environment;
        for (Builtin builtin : Builtins.all()) {
            builtins.put(key(builtin.name()), builtin);
        }
    }

    public Value run(String query) {
        Expr expr = Parser.parse(query);
        check(expr);
        return evaluate(expr);
    }

    /**
     * Rejects anything that would fail no matter what gets rolled, so mistakes are reported before any
     * rolling happens. Values that come out of a roll can only be checked once they exist, which
     * {@link #evaluate} does as it goes.
     */
    private Type check(Expr expr) {
        if (!(expr instanceof Expr.Call call)) {
            // Literals and table names don't depend on any rolls (dice aren't rolled until they're used),
            // so evaluating them is safe.
            return evaluate(expr).type();
        }

        Builtin builtin = lookup(call);
        for (int i = 0; i < call.arguments().size(); i++) {
            Expr argument = call.arguments().get(i);
            Param param = builtin.params().get(i);
            if (argument instanceof Expr.Call inner) {
                Type actual = check(inner);
                if (actual == Type.NUMBER && param.type() == Type.TABLE) {
                    throw new QueryException(
                            "expected a table but " + inner.name() + " gives a number", inner.position());
                }
            } else {
                Value value = evaluate(argument);
                // Dice can always be converted, and converting them here would roll them.
                if (!param.accepts(value.type()) && value.type() != Type.DICE) {
                    Coercion.convert(param.type(), value, environment, argument.position());
                }
            }
        }
        return builtin.returnType();
    }

    private Value evaluate(Expr expr) {
        return switch (expr) {
            case Expr.NumberLiteral number -> new NumberValue(number.value());
            case Expr.TextLiteral text -> new TextValue(text.value());
            case Expr.DiceLiteral dice -> new DiceValue(dice.count(), dice.sides());
            case Expr.Identifier identifier -> new TableValue(
                    Coercion.findTable(environment, identifier.name(), identifier.position()));
            case Expr.Call call -> call(call);
        };
    }

    private Value call(Expr.Call call) {
        Builtin builtin = lookup(call);
        List<Param> params = builtin.params();
        List<Value> values = new ArrayList<>();
        List<Integer> positions = new ArrayList<>();
        for (int i = 0; i < params.size(); i++) {
            Param param = params.get(i);
            if (i < call.arguments().size()) {
                Expr argument = call.arguments().get(i);
                Value value = evaluate(argument);
                if (!param.accepts(value.type())) {
                    value = Coercion.convert(param.type(), value, environment, argument.position());
                }
                values.add(value);
                positions.add(argument.position());
            } else {
                values.add(param.defaultValue());
                positions.add(call.position());
            }
        }
        return builtin.call(new Arguments(values, positions), environment.random());
    }

    private Builtin lookup(Expr.Call call) {
        Builtin builtin = builtins.get(key(call.name()));
        if (builtin == null) {
            throw new QueryException("unknown function " + call.name(), call.position());
        }

        List<Param> params = builtin.params();
        long required = params.stream().filter(param -> !param.isOptional()).count();
        int given = call.arguments().size();
        if (given < required || given > params.size()) {
            throw new QueryException("expected " + builtin.usage() + " but got " + given
                    + (given == 1 ? " argument" : " arguments"), call.position());
        }
        return builtin;
    }

    private static String key(String functionName) {
        return functionName.toLowerCase(Locale.ROOT);
    }
}
