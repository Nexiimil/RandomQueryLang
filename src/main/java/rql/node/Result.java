package rql.node;

/** What a query came to, kept with its type so it can be shown or read back safely. */
public record Result<T>(ValueType<T> type, T value) {
    /** How this is shown to whoever ran the query. */
    public String display() {
        return type.display(value);
    }

    /** Reads the value back, failing if the query produced a different type than expected. */
    public <X> X as(ValueType<X> wanted) {
        if (type != wanted) {
            throw new IllegalStateException("the query gave " + type + ", not " + wanted);
        }
        return wanted.cast(value);
    }
}
