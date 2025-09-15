package QueryLang.Predicates;

import QueryLang.Thing;

public interface IThingPredicate extends IPredicate {
    Thing execute();
}
