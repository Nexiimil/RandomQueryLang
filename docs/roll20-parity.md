# Roll20 parity

RandomQueryLang reads Roll20's dice syntax. This page lists every part of that syntax, how it's supported, and how sure we are that it behaves as Roll20 does. Every row has tests in [`tests/Rql.Tests/Roll20/Roll20ParityTest.cs`](../tests/Rql.Tests/Roll20/Roll20ParityTest.cs), which roll scripted dice so each result is exact.

There's no way to run a query against Roll20 itself: it has no public dice API, and its documentation can't be fetched automatically. So "parity" here means agreeing with what Roll20 documents. **Confidence** says where that comes from, and is the `Parity` trait on each test:

| Confidence | Meaning |
|---|---|
| Documented | Roll20's [Dice Reference](https://help.roll20.net/hc/articles/360037773133), [Order of Operations](https://wiki.roll20.net/Order_of_Operations) or [Roll Query](https://wiki.roll20.net/Roll_Query) pages say so. |
| Community | Roll20 forum posts show it working this way. |
| Recalled | From Roll20's documentation as remembered; not checked against a copy of it. |
| Inferred | The documentation leaves it open; this is the reading that fits it best. |
| Deviation | Deliberately different from Roll20, for the reason given below. |

Run one kind of test with, for example, `dotnet test --filter Parity=Inferred`. If you can check something marked Recalled or Inferred in Roll20, the test is the place to record the answer.

## Syntax

| Syntax | Example | Confidence |
|---|---|---|
| Dice, d with no count | `2d6`, `d20` | Documented |
| Fate dice | `4dF` | Documented |
| No dice | `0d6`, `0d1+10` | Community |
| Arithmetic `+ - * / %` | `2d6+3`, `10%3` | Documented |
| Powers | `2**3`, `4**0.5` | Documented, Community |
| Order: brackets, functions, `**`, `* / %`, `+ -` | `2*3**2` is 18 | Documented |
| Decimals | `7/2` is 3.5 | Recalled |
| `-2**2` is -4, `2**3**2` is 512 | | Inferred |
| `floor` `ceil` `round` `abs` | `round(4.5)` is 5, `round(-2.5)` is -2 | Documented |
| Keep and drop | `8d100k3`, `8d100d3`, `kh` `kl` `dh` `dl` | Documented |
| Keep with no number keeps one | `4d6k` | Inferred |
| Exploding | `3d6!`, `3d6!>5` | Documented |
| Exploding on an exact face | `3d6!3` | Inferred |
| Compounding | `5d6!!` | Documented |
| Penetrating | `5d6!p` | Documented |
| Successes | `3d6>3`, `10d6<4` (ties count) | Documented |
| Failures | `3d6>3f1` | Documented |
| Failures by comparison | `10d6<4f>5` | Recalled |
| Rerolls | `2d8r1`, `2d8r<2`, `2d8r8r2` | Documented |
| Reroll once | `2d8ro<2` | Documented |
| Critical thresholds (display only) | `1d20cs>10`, `1d20cf<3`, `1d20cs20cs10` | Documented, Community |
| Matching | `2d6m`, `2d6mt`, `6d6mt3` | Documented |
| Matching only some faces | `6d6mt>4` | Inferred (the way it's written) |
| Sorting | `8d6s`, `8d6sd` | Documented |
| Groups of several rolls | `{4d6+2d8, 3d20+3, 5d10+1}>40`, `{3d6,4d8}kh1` | Documented, Community |
| Groups of one roll work per die | `{3d6+1}<3`, `{7d20+5}>15`, `{2d6}>8` | Documented, Community |
| Keeping single dice in a group of one roll | `{4d6+3d8}kh1` | Inferred |
| Inline rolls, rolled first | `[[2d6]]d5`, `1d20+[[1d4]]` | Documented |
| Nested inline rolls | `[[ 3d6>[[1+3]] ]]` | Documented |
| Inline labels | `1d20+5[STR]`, `2d6[Fire]` | Recalled |
| Rollable tables | `2t[Flaws]` | Recalled |
| Roll queries | `?{Bonus}`, `?{Bonus\|2}`, `?{Weapon\|Sword,1d8\|Axe,1d10}` | Documented |
| The same question asked once | `?{Bonus\|0} + ?{Bonus\|0}` | Documented |
| HTML entities in roll queries | `&#44;` `&#124;` `&#125;` | Community |
| Roll commands | `/roll`, `/r`, `/gmroll`, `/gr` | Documented |
| Macros | `#Name` | Deviation |
| Character sheet values and abilities | `@{strength}`, `@{Bob\|strength}`, `%{Bob\|Attack}` | Deviation |
| Sheet values put in as text | `@{x}*2` with x = `1+2` is 5 | Recalled |

## Where we differ, and why

- **Macros are saved queries.** `#Name` puts in the saved query called Name from the project. Roll20's macros live in Roll20.
- **Character sheet values are asked for.** There are no character sheets, so `@{...}` and `%{...}` are asked for when the query runs, like a roll query with no default. As in Roll20, the answer is put in as text before anything is rolled.
- **Dividing by 0 is a mistake.** Roll20's math is JavaScript's, where it gives Infinity; here the query stops and says so.
- **Dice have at most 1000 sides**, and one roll can't come to more than 10,000 dice, so nothing rolls forever. Dice that could never stop, like `d6!<6` or `d6r<6`, are refused before rolling.
- **Decimals are exact.** Roll20's JavaScript numbers make `0.1 + 0.2` come to 0.30000000000000004; here it's 0.3.
- **Table names follow RandomQueryLang's rule** (letters, digits and `_`, with `.` for folders), so `t[My Table]` has to be `t[My_Table]`.

## Not covered

- Roll20's chat itself: text around inline rolls, emotes, whispers and the other `/` commands.
- Roll templates (`&{template:...}`) and API commands (`!...`).
- `@{target|...}` and `@{selected|...}` select tokens on a map in Roll20. Here they're asked for, like any other sheet value.
