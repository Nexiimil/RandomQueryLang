package QueryLang;

public class Number implements ITerm {
    private int value;

    public Number(int value) {
        this.value = value;
    }

    @Override
    public String getType() {
        return "Number";
    }

    @Override
    public String displayValue() {
        return Integer.toString(value);
    }

    public int getValue() {
        return value;
    }
}