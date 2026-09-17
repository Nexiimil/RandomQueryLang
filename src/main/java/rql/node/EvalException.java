package rql.node;

/**
 * Something that went wrong while evaluating, blamed on the node responsible rather than on a position
 * in the text. The binder keeps the positions of the nodes it built, so it can turn the blamed node
 * back into a place in the query; a tree built by hand in Java has no positions and keeps the message.
 */
public final class EvalException extends RuntimeException {
    private final transient Node<?> blame;

    public EvalException(Node<?> blame, String message) {
        super(message);
        this.blame = blame;
    }

    /** The node the error is about, which is usually one of the arguments rather than the function. */
    public Node<?> blame() {
        return blame;
    }
}
