package rql.node;

import java.util.List;

/**
 * One step of a query, as a node in a tree. A node knows the type of the value it produces, so a tree
 * that has been built at all is a tree whose types already line up; there is no separate checking pass
 * to keep in step with {@link #evaluate}.
 *
 * <p>Arguments are the components of each node, named and typed, rather than a positional list read
 * back by index.
 */
public interface Node<T> {
    /** The type of the value {@link #evaluate} produces. */
    ValueType<T> type();

    T evaluate(Context context);

    /**
     * The nodes this one is built from, in the order they were written. Enough for any walk over the
     * tree, which is why nodes don't need to be a closed set.
     */
    List<Node<?>> children();

    /**
     * Whether this node's own result depends on a roll. A node whose whole subtree answers {@code false}
     * gives the same answer every time, so it can be worked out before the query runs and any mistake in
     * it reported before anything is rolled.
     */
    default boolean rolls() {
        return false;
    }
}
