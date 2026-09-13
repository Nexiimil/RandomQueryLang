package rql;

/** A problem with a query, pointing at the column of the query text where it was found. */
public class QueryException extends RuntimeException {
    private final int position;

    public QueryException(String message, int position) {
        super(message);
        this.position = position;
    }

    public int getPosition() {
        return position;
    }
}
