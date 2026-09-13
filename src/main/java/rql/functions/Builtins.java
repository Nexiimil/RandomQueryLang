package rql.functions;

import java.util.List;

public final class Builtins {
    private Builtins() {
    }

    public static List<Builtin> all() {
        return List.of(new Roll(), new Pick(), Drop.lowest(), Drop.highest(), new Sum());
    }
}
