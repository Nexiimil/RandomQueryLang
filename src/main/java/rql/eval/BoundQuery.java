package rql.eval;

import java.util.IdentityHashMap;
import java.util.Map;

import rql.node.Node;

/**
 * A tree, with where in the query each of its nodes was written.
 *
 * <p>Positions are kept beside the tree rather than on the nodes, so that a node's components stay
 * only its arguments and a tree built by hand in Java doesn't have to invent them. Nodes are looked up
 * by identity, so two nodes that happen to be equal still have their own positions.
 */
public final class BoundQuery {
    /** Where a node has no recorded position, such as a default the query never wrote. */
    public static final int NOWHERE = -1;

    private final Node<?> root;
    private final Map<Node<?>, Integer> positions;

    BoundQuery(Node<?> root, Map<Node<?>, Integer> positions) {
        this.root = root;
        this.positions = new IdentityHashMap<>(positions);
    }

    public Node<?> root() {
        return root;
    }

    public int positionOf(Node<?> node) {
        return positions.getOrDefault(node, NOWHERE);
    }
}
