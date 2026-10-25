# Changelog
Every change to the RandomQueryLang package that someone using it would notice is written down here, newest first. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Every pull request that changes the package releases a new version of it, published as soon as it's merged. Write what changed under `## [Unreleased]` at the top, in Added, Changed, Deprecated, Removed, Fixed or Security, then run `dotnet run build/Changelog.cs -- version` to turn it into the next version. Anything that could break code using the package goes under Removed, or starts with **Breaking** if it's under Changed.

## [0.1.0] - 2026-10-25

### Added
- The query language as a library: parse and run queries with `Interpreter`, against tables made in code or read from a folder of `.RQLtable` files with `Project`.
- Dice in Roll20's notation, including keep and drop, exploding, rerolls, success counting and groups like `{d20, d20}kh1`.
- Arithmetic, labels, inline rolls and several rolls in one query, separated by `;`.
- The built-in functions `Roll`, `Pick`, `Sum`, `DropHighest`, `DropLowest`, `floor`, `ceil`, `round` and `abs`, and `FunctionRegistry` for adding your own.
- Reading tables from CSV, TSV and JSON, including Roll20's rollable tables, with `TableImport`.
- Saved queries, kept in a project's `saved-queries.json`.
- The package is under the MIT licence.
- `QueryResult.ToString()` gives the same text as `Display()`, so a query of several rolls can be printed.
