package rql.value;

public record NumberValue(int value) implements Value {
    @Override
    public Type type() {
        return Type.NUMBER;
    }

    @Override
    public String display() {
        return Integer.toString(value);
    }
}
