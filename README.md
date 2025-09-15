# RandomQueryLang
A Roll-The-Dice Query Language for video games and table-tops.

## Purpose
The idea of this module is to be able to query a set of tables, much like you would when making a D&D character with d8 flaws, 6 lots of 4d6 frop lowest attributes, etc. all in queries that you can save and use to generate whatever you want on the fly. Just make a set of tables to roll on, and get familiar with the syntax of the query language, and in theory, you should be able to generate some pretty thorough stuff. i'll provide some real-world examples of useful queries to use alongside this as a command-line tool, but I intend to use this for roguelike video games in the future too!

## Format of a Table
A table should take the following form:

Name of thing:[Value of thing as an integer]:[Chance of thing being rolled as a decimal]

Value is a pretty broad term, so to give you some ideas:
- Priority of an item in a queue
- Cost of an item in a game
- 

You can make a table of things without any specified value, or any specified chance, but there are some rules for consistency:
- The value of a thing without a specified value will be the index of the item in the table (how far away the item is from top of the table, starting at 0 with the first thing)
- The chance of a thing being rolled will be equal amongst all things without a specified chance. If there is a table with 3 things, and the first has a chance of 0.5, the remaining 2 items will have a 0.25 chance (remaining 0.5 divided by the remaining 2 items)

This allows the Drop_ commands to work, making sure there is always a method of prioritising things to drop, and allows for faster creation of tables!

## Understanding Queries
Queries are performed in either verbose or symbolic predicates (meaning parts of a sentance). The tables you create/use act as the 'knowledge base' of these predicates, allowing you to have a result. The goal is to be able to execute any roll20 dice roll syntax, as well as 

## Planned Query Elements
Elements in bold have been implemented
- Roll(String/Table table, int x): randomly selects x number items from the provided table.
- Pick(String/Table table, int x): selects an item from the table x number of rows from the top.
- DropLowest(String/Table table, [int x]): returns the rest of the table, having removed the lowest valued x objects. If x is not provided, defaults to 1. In the case that there are more values considered the lowest than x, x number of things are dropped at random.
- DropHighest(String/Table table, [int x]): returns the rest of the table, having removed the highest valued x objects. If x is not provided, defaults to 1. In the case that there are more values considered the lowest than x, x number of things are dropped at random.