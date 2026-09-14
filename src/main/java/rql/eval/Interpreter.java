package rql.eval;

import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

import rql.QueryException;
import rql.functions.Arguments;
import rql.functions.Function;
import rql.functions.Functions;
import rql.functions.Parameter;
import rql.syntax.Expr;
import rql.syntax.Parser;
import rql.value.DiceValue;
import rql.value.NumberValue;
import rql.value.TableValue;
import rql.value.TextValue;
import rql.value.Type;
import rql.value.Value;

public final class Interpreter {
    // Let is handled here rather than being a Function, because it changes what a name means for the
    // query inside it, which only the interpreter can do.
    private static final String LET = "let";
    private static final String LET_USAGE = "Let(name, value, query)";

    private final Environment environment;
    private final Map<String, Function> builtins = new HashMap<>();

    public Interpreter(Environment environment) {
        this.environment = environment;
        for (Function builtin : Functions.all()) {
            builtins.put(key(builtin.name()), builtin);
        }
    }

    public Value run(String query) {
        Expr expr = Parser.parse(query);
        check(expr, Map.of());
        return evaluate(expr, Map.of());
    }

    /**
     * Rejects anything that would fail no matter what gets rolled, so mistakes are reported before any
     * rolling happens. Values that come out of a roll can only be checked once they exist, which
     * {@link #evaluate} does as it goes. {@code variables} holds the type of each name given by Let.
     */
    private Type check(Expr expr, Map<String, Type> variables) {
        return switch (expr) {
            case Expr.Identifier identifier when variables.containsKey(identifier.name()) ->
                    variables.get(identifier.name());
            case Expr.Call call when isLet(call) -> checkLet(call, variables);
            case Expr.Call call -> checkCall(call, variables);
            // Literals and table names don't depend on any rolls (dice aren't rolled until they're used),
            // so evaluating them is safe.
            default -> evaluate(expr, Map.of()).type();
        };
    }

    private Type checkCall(Expr.Call call, Map<String, Type> variables) {
        Function builtin = lookup(call);
        for (int i = 0; i < call.arguments().size(); i++) {
            Expr argument = call.arguments().get(i);
            Parameter param = parameter(builtin, i);
            if (param.isQuery()) {
                check(argument, variables);
            } else if (argument instanceof Expr.Call || isVariable(argument, variables)) {
                Type actual = check(argument, variables);
                if (actual == Type.NUMBER && param.type() == Type.TABLE) {
                    String source = argument instanceof Expr.Call inner
                            ? inner.name() + " gives"
                            : ((Expr.Identifier) argument).name() + " is";
                    throw new QueryException(
                            "expected a table but " + source + " a number", argument.position());
                }
            } else {
                Value value = evaluate(argument, Map.of());
                // Dice can always be converted, and converting them here would roll them.
                if (!param.accepts(value.type()) && value.type() != Type.DICE) {
                    Coercion.convert(param.type(), value, environment, argument.position());
                }
            }
        }
        return builtin.returnType();
    }

    private Type checkLet(Expr.Call call, Map<String, Type> variables) {
        String name = letName(call);
        Type type = check(call.arguments().get(1), variables);
        // Dice given to Let are rolled once, so the name stands for a table of the faces they landed on.
        if (type == Type.DICE) {
            type = Type.TABLE;
        }
        return check(call.arguments().get(2), with(variables, name, type));
    }

    private Value evaluate(Expr expr, Map<String, Value> variables) {
        return switch (expr) {
            case Expr.NumberLiteral number -> new NumberValue(number.value());
            case Expr.TextLiteral text -> new TextValue(text.value());
            case Expr.DiceLiteral dice -> new DiceValue(dice.count(), dice.sides());
            case Expr.Identifier identifier when variables.containsKey(identifier.name()) ->
                    variables.get(identifier.name());
            case Expr.Identifier identifier -> new TableValue(
                    Coercion.findTable(environment, identifier.name(), identifier.position()));
            case Expr.Call call when isLet(call) -> let(call, variables);
            case Expr.Call call -> call(call, variables);
        };
    }

    private Value let(Expr.Call call, Map<String, Value> variables) {
        String name = letName(call);
        Value value = evaluate(call.arguments().get(1), variables);
        if (value instanceof DiceValue dice) {
            value = new TableValue(dice.roll(environment.random()));
        }
        return evaluate(call.arguments().get(2), with(variables, name, value));
    }

    private Value call(Expr.Call call, Map<String, Value> variables) {
        Function builtin = lookup(call);
        List<Expr> given = call.arguments();
        int count = Math.max(given.size(), builtin.params().size());
        Arguments.Builder arguments = Arguments.builder();
        for (int i = 0; i < count; i++) {
            Parameter param = parameter(builtin, i);
            if (i < given.size()) {
                Expr argument = given.get(i);
                if (param.isQuery()) {
                    arguments.addQuery(() -> evaluate(argument, variables), argument.position());
                } else {
                    Value value = evaluate(argument, variables);
                    if (!param.accepts(value.type())) {
                        value = Coercion.convert(param.type(), value, environment, argument.position());
                    }
                    arguments.addValue(value, argument.position());
                }
            } else if (param.isOptional()) {
                arguments.addValue(param.defaultValue(), call.position());
            }
        }
        return builtin.call(arguments.build(), environment.random());
    }

    private Function lookup(Expr.Call call) {
        Function builtin = builtins.get(key(call.name()));
        if (builtin == null) {
            throw new QueryException("unknown function " + call.name(), call.position());
        }

        List<Parameter> params = builtin.params();
        long required = params.stream().filter(param -> !param.isOptional() && !param.isRepeated()).count();
        boolean repeats = !params.isEmpty() && params.getLast().isRepeated();
        int given = call.arguments().size();
        if (given < required || (!repeats && given > params.size())) {
            throw argumentCountError(builtin.usage(), given, call.position());
        }
        return builtin;
    }

    // Arguments past the last parameter belong to it, which lookup only allows when it's repeated.
    private static Parameter parameter(Function builtin, int index) {
        List<Parameter> params = builtin.params();
        return index < params.size() ? params.get(index) : params.getLast();
    }

    private static String letName(Expr.Call call) {
        int given = call.arguments().size();
        if (given != 3) {
            throw argumentCountError(LET_USAGE, given, call.position());
        }
        Expr name = call.arguments().getFirst();
        if (!(name instanceof Expr.Identifier identifier)) {
            throw new QueryException("expected a name for the value, like Let(strength, ...)", name.position());
        }
        return identifier.name();
    }

    private static QueryException argumentCountError(String usage, int given, int position) {
        return new QueryException(
                "expected " + usage + " but got " + given + (given == 1 ? " argument" : " arguments"), position);
    }

    private static boolean isLet(Expr.Call call) {
        return key(call.name()).equals(LET);
    }

    private static boolean isVariable(Expr expr, Map<String, Type> variables) {
        return expr instanceof Expr.Identifier identifier && variables.containsKey(identifier.name());
    }

    // A name given by Let only stands for its value inside the query it was given for.
    private static <T> Map<String, T> with(Map<String, T> variables, String name, T value) {
        Map<String, T> inner = new HashMap<>(variables);
        inner.put(name, value);
        return inner;
    }

    private static String key(String functionName) {
        return functionName.toLowerCase(Locale.ROOT);
    }
}
