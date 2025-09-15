package QueryLang.Predicates;

import QueryLang.Tables.Table;

public interface ITablePredicate extends IPredicate {
    Table execute();
}
