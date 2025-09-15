import java.util.List;
import java.util.Scanner;

import QueryLang.Predicates.IPredicate;
import QueryLang.Predicates.PredicateFactory;
import QueryLang.Tables.Table;
import QueryLang.Tables.TableProvider;

public class Main {
    public static void main(String[] args) {

        TableProvider tableProvider = new TableProvider();
        List<Table> tables = tableProvider.loadTables();

        System.out.println("This is RandomQueryLang! You have the following tables available from this directory:");

        for (Table table : tables) {
            System.out.println(" - " + table.getName());
        }

        System.out.println("Please enter your query:");
        // Read user input
        Scanner scanner = new Scanner(System.in);
        String query = scanner.nextLine();

        // Process the query
        IPredicate results = PredicateFactory.ProcessQuery(query);
    }
}
