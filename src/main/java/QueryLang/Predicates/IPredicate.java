package QueryLang.Predicates;

import QueryLang.ITerm;

public interface IPredicate extends ITerm {
    String getSymbol();
    String getPredicateName();
}
