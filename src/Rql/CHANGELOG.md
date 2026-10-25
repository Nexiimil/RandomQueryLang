# Changelog
Every change to the RandomQueryLang package that someone using it would notice is written down here, newest first. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

A change waits under [Unreleased] until a release moves it under that release's version. Entries go under one of Added, Changed, Deprecated, Removed, Fixed or Security. Anything that could break code using the package goes under Removed, or starts with **Breaking** if it's under Changed, and needs a major release (a minor one before 1.0).

## [Unreleased]

### Added
- The query language as a library: parse and run queries with `Interpreter`, against tables made in code or read from a folder of `.RQLtable` files with `Project`.
- Dice in Roll20's notation, including keep and drop, exploding, rerolls, success counting and groups like `{d20, d20}kh1`.
- Arithmetic, labels, inline rolls and several rolls in one query, separated by `;`.
- The built-in functions `Roll`, `Pick`, `Sum`, `DropHighest`, `DropLowest`, `floor`, `ceil`, `round` and `abs`, and `FunctionRegistry` for adding your own.
- Reading tables from CSV, TSV and JSON, including Roll20's rollable tables, with `TableImport`.
- Saved queries, kept in a project's `saved-queries.json`.
- `QueryResult.ToString()` gives the same text as `Display()`, so a query of several rolls can be printed.
