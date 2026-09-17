package rql.eval;

import rql.QueryException;
import rql.node.Context;
import rql.node.EvalException;
import rql.node.Node;
import rql.node.Result;
import rql.syntax.Expr;
import rql.syntax.Parser;

/**
 * Runs a query: read it, build the tree, report what can be reported without rolling, then roll.
 *
 * <p>There is no type checking here. A tree the binder produced is a tree whose types line up, so the
 * only thing left to go wrong is something that depends on what comes up.
 */
public final class Interpreter {
    private final Context context;

    public Interpreter(Context context) {
        this.context = context;
    }

    public Result<?> run(String query) {
        Expr expr = Parser.parse(query);
        BoundQuery bound = Binder.bind(expr, context.tables());
        Check.beforeRolling(bound, context.tables());
        return evaluate(bound.root(), bound);
    }

    private <T> Result<T> evaluate(Node<T> node, BoundQuery bound) {
        try {
            return new Result<>(node.type(), node.evaluate(context));
        } catch (EvalException e) {
            throw new QueryException(e.getMessage(), bound.positionOf(e.blame()));
        }
    }
}
