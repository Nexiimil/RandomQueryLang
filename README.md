# RandomQueryLang
A Roll-The-Dice Query Language for video games and table-tops.

## Purpose
The idea of this module is to be able to query a set of tables, much like you would when making a D&D character with d8 flaws, 6 lots of 4d6 frop lowest attributes, etc. all in queries that you can save and use to generate whatever you want on the fly. Just make a set of tables to roll on, and get familiar with the syntax of the query language, and in theory, you should be able to generate some pretty thorough stuff. i'll provide some real-world examples of useful queries to use alongside this as a command-line tool, but I intend to use this for roguelike video games in the future too!

## Building and Running
You need Java 21 or newer. Maven doesn't need to be installed, as the wrapper downloads it the first time it runs.

```
./mvnw package                        # compile, run the tests and build target/rql.jar
java -jar target/rql.jar [directory]  # start the query prompt
```

On Windows, use `mvnw.cmd` instead of `./mvnw`. Tables are loaded from the current directory unless you give a directory.

## Format of a Table
A table is a file ending in `.RQLtable`, and the file name (without the extension) is the table's name. Each line of the file is one thing, in the form:

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
- Numbers: whole numbers, like `4` or `-1`.
- Text: written in double quotes, like `"Weapon Types"`. Inside text, write `\"` for a quote and `\\` for a backslash.
- Tables: a table's name written without quotes, like `Flaws`. Rolling, picking and dropping all result in tables too, which is what lets them be nested.
- Dice: written like `d20` for one twenty-sided die, or `4d6` for four six-sided dice. Dice aren't tables; each die is rolled with a random number every time it's used, so it can have as many sides as you like. A query that's only dice, like `4d6`, isn't rolled; use `Roll(4d6)`.

When a function is given a different type than it needs, the value is converted where that makes sense:
- Text that is a whole number can be used as a number, like `Roll(d6, "3")`.
- Text can be used as the name of a table, which is handy for names with spaces, like `Roll("Weapon Types", 1)`.
- A table with exactly one thing in it can be used as that thing's value (as a number) or its name (as text), like `Roll(Flaws, Roll(d4, 1))`.
- A table with more than one thing can't be used as a number. Use `Sum` to add it up.
- Dice used as a table are rolled, giving one row per die worth the number it landed on. `Sum(DropLowest(4d6))` rolls 4d6, drops the lowest die and adds up the rest.
- Dice used as a number are rolled and added up, so `Roll(Flaws, d4)` rolls on Flaws between 1 and 4 times.

Mistakes that would fail no matter what gets rolled, such as a table that doesn't exist or `Roll(d6, "lots")`, are reported before anything is rolled. Anything that depends on a roll can only be checked once it's been rolled.

Function names ignore case, so `roll` works as well as `Roll`. Table names must match exactly.

## Planned Query Elements
Elements in bold have been implemented
- **Roll(Table/Dice table, [Number x])**: randomly selects x number items from the provided table, rolling separately for each one, so the same thing can come up more than once. If x is not provided, defaults to 1. Rolling dice x times gives a row for every die, so `Roll(4d6, 2)` gives 8 rows.
- **Pick(Table table, Number x)**: selects the xth item from the top of the table, so `Pick(table, 1)` is the first thing.
- **DropLowest(Table table, [Number x])**: returns the rest of the table, having removed the lowest valued x objects. If x is not provided, defaults to 1. In the case that there are more values considered the lowest than x, x number of things are dropped at random.
- **DropHighest(Table table, [Number x])**: returns the rest of the table, having removed the highest valued x objects. If x is not provided, defaults to 1. In the case that there are more values considered the highest than x, x number of things are dropped at random.
- **Sum(Table table)**: adds up the values of every thing in the table.
