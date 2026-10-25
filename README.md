# RandomQueryLang
A Roll-The-Dice Query Language for video games and table-tops.

## Purpose
The idea of this module is to be able to query a set of tables, much like you would when making a D&D character with d8 flaws, 6 lots of 4d6 frop lowest attributes, etc. all in queries that you can save and use to generate whatever you want on the fly. Just make a set of tables to roll on, and get familiar with the syntax of the query language, and in theory, you should be able to generate some pretty thorough stuff. i'll provide some real-world examples of useful queries to use alongside this as a command-line tool, but I intend to use this for roguelike video games in the future too!

## Building and Running
You need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```
dotnet test                                           # build everything and run the tests
dotnet run --project src/Rql.Web                      # the web app, at http://localhost:5051
dotnet run --project src/Rql.Web -- --project DIR     # ...opening DIR as the project (made if it isn't there)
dotnet run --project src/Rql.Cli -- [DIR]             # the command-line query prompt
```

The `examples` folder is a small project to try: `dotnet run --project src/Rql.Web -- --project examples`.

### Projects
A project is a folder of tables. Tables are found in it and in every folder inside it (hidden folders, like `.git`, are skipped), so you can organise them however you like. A table in a folder is named by its path from the top of the project with dots between the folders, the way .NET names a class in a namespace: `Monsters/Undead.RQLtable` is `Monsters.Undead`, and is written just like that in a query, `Roll(Monsters.Undead)`. A query can also call it just `Undead`, as long as no other table's name ends the same way. If two do, the query says which ones, and you write as much of the path as tells them apart.

Every table can be written in a query just as it's named, so table and folder names inside a project can only use letters, digits and `_`: no spaces or dots. They can't start with a digit, or look like dice (`d6`). Use `WeaponTypes` or `Weapon_Types` rather than `Weapon Types`. A file or folder that breaks this, like `Weapon Types.RQLtable` or `Weapon.Types.RQLtable`, is listed as a problem rather than read as something else. The project's own folder can be called anything.

Queries you save are kept in the project too, in `saved-queries.json`, so they travel with the tables. It's plain JSON, fine to edit or share by hand.

The web app remembers the projects you've opened, and starts with the last one. Its list is kept in your user settings folder (`~/.config/RandomQueryLang` on Linux, `%APPDATA%\RandomQueryLang` on Windows).

### The web app
- **Roll**: type a query and roll it. Mistakes are pointed out as you type, before anything is rolled. Results stay in a history you can roll again from. A link like `/?q=Roll(Flaws, d4)` rolls that query when it's opened.
- **Saved queries**: press **Save** beside a query to keep it under a name. The saved queries panel lists them; click one to roll it, or type to find one and press Enter to roll the first match. ✎ puts a query back in the box to change it, and saving it again under the same name updates it. A saved query that no longer works, because a table it uses was renamed say, is marked with ⚠.
- **Tables**: make, edit, move, rename and delete tables, grouped by folder. The editor shows what each row's value and chance come to as you fill it in, and lets you try rolling on the table before you save it. The **Text** tab edits the same table as file text, which is quicker for pasting in a long list, and is where a file that can't be read opens so you can fix it.
- **Projects** (the project button, top right): reopen a recent project, or browse to any folder on this computer to open it, or make a new folder for a new project.

The web app reads and writes files anywhere on the computer it runs on, so it's meant to be run for yourself. It only listens on `localhost` unless you tell it otherwise; don't put it on a network others can reach.

The command line works on the same projects. Saved queries can be run there by typing `@` and their name, like `@Stats`.

### Importing tables
Tables can be brought in from CSV, TSV and JSON, or from another project's `.RQLtable` files. In the web app, use **Import** on the Tables page: pick files or paste text, look over each table found (its name, a preview of its rows and what each comes up as), choose a folder, and import. A table that's already there is only replaced if you tick it to be. From the command line:

```
dotnet run --project src/Rql.Cli -- import loot.csv bestiary.json --project DIR --folder Monsters [--replace]
```

- **CSV** with a header row finds its columns by name: `name` (or item, result), `value`, `chance` (0 to 1, or a percentage like `25%`) or `weight`. Other columns are left out. Without a header, columns are name, value and chance, as in a table file. Fields can be separated by commas, semicolons or tabs, and quoted.
- **JSON** can be a list of names (`["Sword", "Shield"]`), a list of rows (`[{"name": "Sword", "value": 10, "weight": 2}]`), a table (`{"name": "Loot", "items": [...]}`, which is how Roll20 gives a rollable table), or several tables, as a list or under `"tables"`.
- **Weights** work as in Roll20's rollable tables: a row of weight 2 comes up twice as often as one of weight 1, and a row with no weight counts as 1. They're turned into chances when imported.
- Names are made into names a table can have: a file called `Weapon Types.csv` becomes `Weapon_Types`, and a line break inside a CSV cell becomes a space.

### Layout
- `src/Rql`: the language: parsing, the node tree, the functions, and reading and saving tables and saved queries. A game uses this.
- `src/Rql.Web`: the Blazor web app.
- `src/Rql.Cli`: the command-line prompt.
- `tests`: tests for the language (`Rql.Tests`) and the web app's pages (`Rql.Web.Tests`).

## Format of a Table
A table is a file ending in `.RQLtable`, and the file name (without the extension) is the table's name, with any folders it's in at the front, separated by dots, like `Monsters.Undead`. Each line of the file is one thing, in the form:

Name of thing;[Value of thing as an integer];[Chance of thing being rolled as a decimal]

For example:

```
Sword;10;0.1
Shield;5
Potion;;0.5
Rope
```

Value is a pretty broad term, so to give you some ideas:
- Priority of an item in a queue
- Cost of an item in a game
- 

You can make a table of things without any specified value, or any specified chance, but there are some rules for consistency:
- The value of a thing without a specified value will be the position of the item in the table (how far down the table the item is, starting at 1 with the first thing, the same way `Pick` counts). The exception is a thing whose name is a whole number, which takes that number as its value, so a table of `1` to `6` works like a d6.
- The chance of a thing being rolled will be equal amongst all things without a specified chance. If there is a table with 3 things, and the first has a chance of 0.5, the remaining 2 items will have a 0.25 chance (remaining 0.5 divided by the remaining 2 items)
- To give a thing a chance but no value, leave the value empty, like `Potion;;0.5`.
- Blank lines are ignored.
- A table is rejected if its chances add up to more than 1, or add up to exactly 1 while some things have no chance (those things could never be rolled).

This allows the Drop_ commands to work, making sure there is always a method of prioritising things to drop, and allows for faster creation of tables!

## Understanding Queries
Queries are performed in either verbose or symbolic predicates (meaning parts of a sentance). The tables you create/use act as the 'knowledge base' of these predicates, allowing you to have a result. The goal is to be able to execute any roll20 dice roll syntax, as well as 

For now, queries are written as function calls, which can be nested inside each other:

```
Roll(Flaws, d4)
Sum(DropLowest(4d6))
```

### Types
Every query, and every part of a query, results in one of four types:
- Numbers: like `4`, `-1` or `2.5`. Table values are always whole.
- Text: written in double quotes, like `"Sword of Doom"`. Inside text, write `\"` for a quote and `\\` for a backslash.
- Tables: a table's name, written as it is, like `Flaws`, including any folders, like `Monsters.Undead`. Rolling, picking and dropping all result in tables too, which is what lets them be nested.
- Dice: written like `d20` for one twenty-sided die, or `4d6` for four six-sided dice, with any of Roll20's modifiers after them (see [Dice](#dice)). Dice aren't tables; they're rolled fresh every time they're used. A die can have up to 1000 sides. A query that's only dice, like `4d6kh3`, is rolled, and comes to its total.

When a function is given a different type than it needs, the value is converted where that makes sense:
- Text that is a number can be used as a number, like `Roll(d6, "3")`.
- Text can be used as the name of a table, like `Roll("Flaws", 1)`, but only a name the table could have been written as anyway: `Roll("Weapon Types")` is an error, since no table can have a space in its name.
- A table with exactly one thing in it can be used as that thing's value (as a number) or its name (as text), like `Roll(Flaws, Roll(d4, 1))`.
- A table with more than one thing can't be used as a number. Use `Sum` to add it up.
- Dice used as a table are rolled, giving one row per die kept, worth the number it landed on. `Sum(DropLowest(4d6))` rolls 4d6, drops the lowest die and adds up the rest, which is the same as `4d6kh3`.
- Dice used as a number are rolled and added up (or, with a success target, their successes counted), so `Roll(Flaws, d4)` rolls on Flaws between 1 and 4 times.

Mistakes that would fail no matter what gets rolled, such as a table that doesn't exist or `Roll(d6, "lots")`, are reported before anything is rolled. Anything that depends on a roll can only be checked once it's been rolled.

Function names ignore case, so `roll` works as well as `Roll`. Table names must match exactly.

### Arithmetic
Numbers, and anything that can be a number, can be added, subtracted, multiplied, divided and raised to powers: `d20 + 5`, `2d6 * 2 - 1`, `2**3`, `Sum(4d6) + Pick(Flaws, 1)`. As in Roll20, brackets come first, then functions, then `**`, then `*`, `/` and `%` (the remainder), then `+` and `-`. Numbers can have decimals, so `7 / 2` is 3.5; round them with `floor`, `ceil`, `round` (halves go up) and `abs`. Anything that has to be whole, like a number of dice or the row to `Pick`, says so if it isn't.

### Several rolls at once
Separate rolls with `;` to make them all in one query, and give any of them a label with a name and a colon:

```
Attack: d20 + 5; Damage: 2d6 + 3
Stats: 4d6kh3; 4d6kh3; 4d6kh3; 4d6kh3; 4d6kh3; 4d6kh3
```

Each roll gets its own result, along with the dice that went into it. A mistake in any of them stops the whole query before anything is rolled.

### Dice
Dice follow [Roll20's notation](https://help.roll20.net/hc/en-us/articles/360037773133-Dice-Reference). After the dice come any of these, where `>` means "at least" and `<` means "at most", as in Roll20:

| Written | Does |
|---|---|
| `4d6kh3`, `4d6k3` | Keep the highest 3 dice. `kl` keeps the lowest. |
| `4d6dl1`, `4d6d1` | Drop the lowest die. `dh` drops the highest. |
| `d6!` | Exploding: a 6 rolls another die, and so on. `d6!>5` explodes on 5 or more, `d6!5` on exactly 5. |
| `d6!!` | Compounding: the extra rolls add onto the die that exploded, rather than being dice of their own. |
| `d6!p` | Penetrating: like `!`, but every extra die takes 1 off what it rolled. |
| `d20r1`, `d20r<2` | Reroll a 1 (or 2 or less) until it isn't. Several `r`s can be given. `ro` rerolls only once. |
| `5d10>8` | Count successes: the result is how many dice came up 8 or more. `=10` counts exact 10s. |
| `5d10>8f1` | Also take away a success for each 1. |
| `4d6s`, `4d6sd` | Sort the dice, lowest or highest first. |
| `4dF` | Fate dice, each -1, 0 or 1. |

| `1d20cs>18`, `1d20cf<2` | Count 18 or more as a critical success, or 2 or less as a critical failure. This only changes how dice are shown; without them, the highest and lowest faces are the criticals. |
| `4d6m`, `4d6mt`, `6d6mt3` | Matching: `m` picks out dice that came up the same, and `mt` makes the result how many sets there are (of at least 3, with `mt3`). |
| `0d6` | No dice at all, which comes to 0; handy in a group. |

Dice that would never stop rolling, like `d6!<6` or `d6r<6`, are refused, and no roll can come to more than 10,000 dice.

### Groups, inline rolls, labels and tables
- **Groups**, in braces, keep, drop or count among several rolls: `{d20, d20}kh1` keeps the better of two d20s, and `{4d6+2d8, 3d20+3}>40` counts the rolls coming to 40 or more. A group of one roll works on its dice instead: `{3d20+5}>21` adds 5 to each d20 and counts those reaching 21.
- **Inline rolls**, in double square brackets, are rolled before anything else and put in place as a number, so they can be anywhere a number can: `[[1d4]]d6` rolls 1d4 to decide how many d6 to roll, and `3d6>[[1+3]]` counts dice of 4 or more.
- **Labels**, in square brackets after a term, name it without changing it: `1d20 + 5[STR] + 2[Proficiency]`. They're shown beside the result.
- **Tables** can be rolled Roll20's way as well as with `Roll`: `2t[Flaws]` is `Roll(Flaws, 2)`.

### Asking when rolling
- **Roll queries** ask for something each time the query runs: `?{Bonus}`, `?{Bonus|2}` with a default, or `?{Weapon|Sword,1d8|Axe,1d10}` to pick from options. The same question is only asked once in a query. As in Roll20, the first `}` ends a roll query, so a `|`, `,` or `}` inside an option is written `&#124;`, `&#44;` or `&#125;`.
- **Character sheet values**, `@{strength}` or `@{Bob|strength}`, and abilities, `%{Bob|Attack}`, are asked for too, since there are no character sheets to read them from.
- **Macros**: `#Name` puts in the saved query called Name, so saved queries can be built from each other.
- A `/roll` or `/r` in front, as pasted from Roll20's chat, is ignored.

Like Roll20, all of these are put in place as text before anything is rolled, in this order: macros, then character sheet values, then roll queries, then inline rolls, innermost first. The web app asks with a form, offering last time's answers; the command line asks in the terminal.

See [docs/roll20-parity.md](docs/roll20-parity.md) for how closely each part matches Roll20, and where it differs on purpose.

Every result shows the dice rolled for it. The command line writes them out like `4d6kh3: 5, (1), 5, 4 = 14`, with dropped dice in brackets, `!` on dice from explosions, `2→5` for a 2 rerolled into a 5, and `*` on successes. The web app shows each die, with dropped ones struck through and highest and lowest faces coloured.

### Adding your own functions
A function is a record whose constructor's parameters are its arguments. Mark it with `[Function]`, give any optional argument a `[Default]`, and add it to a `FunctionRegistry`:

```csharp
[Function("Repeat", Description = "Writes text out several times.")]
public sealed record Repeat(Node<string> Text, [Default(2)] Node<int> Times) : Function<string>
{
    public override string Evaluate(Context context) =>
        string.Concat(Enumerable.Repeat(Text.Evaluate(context), Times.Evaluate(context)));
}

var interpreter = new Interpreter(context, FunctionRegistry.Builtins().Add<Repeat>());
```

That's all of it: how the function is written in a query (`Repeat(text, [times])`), what types it takes, and how its arguments are converted are all read from the record. A function that can't work, such as one with a parameter that isn't a `Node`, is reported when it's added rather than when a query first uses it. Several functions can share a name if each takes a different type first, as the two `Roll`s do. If a function rolls anything, override `Rolls` to return `true`, so the query isn't checked by running it early.

## Planned Query Elements
Elements in bold have been implemented
- **Roll(Table/Dice table, [Number x])**: randomly selects x number items from the provided table, rolling separately for each one, so the same thing can come up more than once. If x is not provided, defaults to 1. Rolling dice x times gives a row for every die, so `Roll(4d6, 2)` gives 8 rows.
- **Pick(Table table, Number x)**: selects the xth item from the top of the table, so `Pick(table, 1)` is the first thing.
- **DropLowest(Table table, [Number x])**: returns the rest of the table, having removed the lowest valued x objects. If x is not provided, defaults to 1. In the case that there are more values considered the lowest than x, x number of things are dropped at random.
- **DropHighest(Table table, [Number x])**: returns the rest of the table, having removed the highest valued x objects. If x is not provided, defaults to 1. In the case that there are more values considered the highest than x, x number of things are dropped at random.
- **Sum(Table table)**: adds up the values of every thing in the table.
