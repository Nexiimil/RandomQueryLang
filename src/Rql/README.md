# RandomQueryLang
A query language for rolling dice and rolling on tables, for video games and table-tops. Dice are written in Roll20's notation (`d20`, `4d6kh3`, `{d20, d20}kh1`), and tables are lists of things with a value and a chance of coming up.

```csharp
using Rql.Evaluation;
using Rql.Nodes;
using Rql.Projects;
using Rql.Tables;

// Tables can be made in code...
var loot = new Table("Loot", [new("Sword", 10, 0.5), new("Shield", 5, 0.3), new("Gem", 50, 0.2)]);
var rql = new Interpreter(new Context(new Dictionary<string, Table> { ["Loot"] = loot }, Random.Shared));

// ...or read from a folder of .RQLtable files
var tables = new Project("path/to/tables").Tables.LoadAll().Tables;
var fromFiles = new Interpreter(new Context(tables, Random.Shared));

Table rolled = rql.Run("Roll(Loot, 3)").As<Table>();
decimal strength = rql.Run("Sum(DropLowest(4d6))").As<decimal>();

// Several rolls at once, each with its own label
foreach (Result result in rql.Run("Attack: d20 + 5; Damage: 2d6 + 3").Results)
{
    Console.WriteLine($"{result.Label}: {result.Display()}");
}
```

Pass a `Random` with a fixed seed to get the same rolls every time. A query with a mistake in it, such as a table that isn't there, throws a `QueryException` saying what's wrong and where; `Interpreter.Validate` checks a query without rolling it.

The table file format, the full query language and how to add your own functions are described in the [project's README](https://github.com/Nexiimil/RandomQueryLang#readme).
