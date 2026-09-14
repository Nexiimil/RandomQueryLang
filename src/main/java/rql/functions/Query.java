package rql.functions;

import rql.value.Value;

/**
 * Part of a query given to a function without being run, for parameters made with
 * {@link Parameter#query}. Every run rolls again, so the function decides how many rolls happen.
 */
@FunctionalInterface
public interface Query {
    Value run();
}
