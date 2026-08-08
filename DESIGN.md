# Gauntlet Design

The gauntlet runs real schemas and real transitions through NSchema against real databases, on every
supported engine, and reports what worked, what broke, and what the engine cannot do.

It exists because NSchema's fidelity claims are only worth what they have been tested against. Fixtures
prove the code does what it was written to do. The gauntlet proves it survives schemas written by people
who have never heard of NSchema.

## What it proves

Two independent things:

- **Fidelity** — NSchema can describe a schema that already exists, without loss and without inventing
  differences.
- **Transition** — NSchema can move a database from one schema to another, correctly, and says so
  honestly when the engine will not let it.

Everything below is in service of one of those two.

## Principles

- **A limitation NSchema does not report is a bug.** SQLite cannot alter a key on an existing table. The
  correct behaviour is a diagnostic at plan time, not a syntax error at apply time. The gauntlet asserts
  the diagnostic, so an engine limitation is a documented capability rather than a red cell.
- **Acquisition is not part of the run.** Anything fragile, networked, or dependent on someone else's
  toolchain happens ahead of time and lands as a pinned file. A gauntlet run is hermetic.
- **Acquired states arrive in engine DDL; authored states are written in NSQL.** A corpus schema is taken
  as-is from upstream. A scenario is authored once and runs on every engine.
- **Coverage is derived, not declared.** What a case covers is read off the plan it produces, so it cannot
  go stale.
- **Adding a case is dropping a directory in.** No code, no registration. The twentieth case is where the
  interesting bugs are, and it only gets written if it is free to add.
- **Everything the matrix is built from is a catalog: `Names`, and `Get(name)`.** Engines, scenarios and
  corpora all answer the same two questions, whether they are registered in code or by existing on disk.
  Enumerating a catalog materialises nothing, so the matrix can be built at discovery time. A *fleet* sits
  over a catalog only where materialising is expensive enough to want reuse — which is why engines have one
  and cases do not.
- **Schema only, never data.** Except for the small row seeds a scenario needs to exercise a data hazard.

## Case types

Two entry points over one engine.

### Corpus

A real schema, acquired from outside, in that engine's own DDL.

```
corpus/chinook/
  postgres.sql        # upstream script, unmodified
  sqlserver.sql
  sqlite.sql
  manifest.json
```

`manifest.json` records, per engine: upstream source, the commit or release it was taken from, its
licence, and when it was captured. A case supplies DDL for the engines it has; missing engines are
absent from the matrix rather than failing in it.

Corpora exercise **fidelity**, plus the two transitions to and from empty.

### Scenario

An authored pair of states, testing one capability.

```
scenarios/foreign-key-retarget/
  before.nsql
  after.nsql
  data.sql            # optional, engine DDL, small
  before.postgres.sql # optional override, engine DDL
  manifest.json
```

Scenarios exercise **transition** only. They are where engine limitations surface, because a corpus only
ever creates from empty and drops to empty — it never alters anything.

A scenario writes `{schema}` where its objects' schema goes, and the engine substitutes its own — `public`,
`main`, `dbo`. Without it a scenario about foreign keys fails on SQLite over the schema name instead, and
the limitation it was written to find never gets reached. A scenario that hardcodes a schema is declaring
itself engine-specific.

## Primitives

### Fidelity(S)

Given a database seeded to schema `S`:

1. **Import** — `nschema import` produces a project. *Introspection and the writer produce parseable output.*
2. **Format** — formatting the imported project is a no-op, and formatting twice is identical.
   *The writer emits canonical text.*
3. **Reparse** — parsing the project back to a model equals the introspected model.
   *The writer/parser round trip is lossless.*
4. **Plan against self** — refresh state from the database, plan against the imported project, and the plan
   is empty. *The comparer does not hallucinate differences.*

Stage 4 is expected to have the highest yield. Phantom diffs on real schemas are where round-trip tools
bleed.

### Transition(A → B)

Given a database seeded to schema `A`:

1. **Plan** `A → B`.
2. **Assert** the plan and its diagnostics against approved snapshots.
3. **Apply**, expecting success or failure according to what the diagnostics predicted.
4. **Introspect** and compare the result to `B`.

A corpus case composes these: `Fidelity(S)`, `Transition(Empty → S)`, `Transition(S → Empty)`. The last one
is not ceremony — it is the only test of drop ordering.

**Three of the legs compare NSchema to NSchema.** Fidelity diffs a project against state, and both sides came
from the same introspection; rebuilding applies the rendered SQL and reads the result back with the same
introspector. So an introspection error that is *consistent* — a type read back as something it isn't, but
read back that way every time — is invisible to those checks, and the case would stay green while NSchema
builds the wrong database. The **faithful** leg is the oracle outside NSchema: after a rebuild completes,
the same catalog queries run against source and rebuild — `information_schema`/`sys`/`sqlite_master`
inventories, one ordered row per fact — and the two accounts are diffed. NSchema is nowhere in that loop, and
the inventory is deliberately broader than NSchema's model (every routine kind, for one), because what
NSchema cannot see is exactly what it exists to catch. A green cell now means the engine itself agrees.

These are the two shapes a run has, not two types in the code. A scenario carries its own before and after
and is run by `ScenarioRunner`; whether the corpus shares that code or gets its own runner is for the
corpus to decide when it arrives, rather than something to abstract for a second user that does not exist.

## Setup versus assertion

Establishing a before-state is setup, not assertion. Restoring corpus DDL, applying a scenario's
`before.nsql`, loading `data.sql` — a failure in any of these is reported as a **setup failure** and the
case's assertions are not run.

This matters for attribution. A scenario's before-state is established by applying NSQL through NSchema
itself, so without the distinction a bug in `Empty → before` reads as a failure of the transition under
test.

## Assertions

A transition asserts four things, in this order:

- **The plan** — a Verify snapshot per engine. The end state being right is not sufficient: retargeting a
  foreign key by dropping and recreating it lands the correct schema while being the wrong behaviour.
- **The diagnostics** — a Verify snapshot per engine. This is where engine limitations live.
- **The apply** — succeeds, or fails exactly as the diagnostics said it would.
- **The end state** — the introspected schema equals **where the transition was supposed to leave the
  database**: the target if it applied, the state it started from if it was refused. A refusal has to prove
  it was clean; refusing and then leaving the database half-migrated is the failure a limitation cell would
  otherwise hide.

Snapshots are bidirectional by construction: an unexpected diagnostic fails, and so does the disappearance
of an expected one. A limitation cannot be silently dropped.

**A matching snapshot alone is not a limitation.** Approving a snapshot that contains an error would otherwise
turn a bug into documentation — the harness cannot tell them apart from the diagnostics alone. A blocked cell
is a *limitation* only when the scenario's manifest declares it for that engine, naming the capability gap it
stems from; an undeclared block fails as a bug, and a declared limitation that no longer blocks fails as stale
documentation. The declared reason rides the snapshot, so the capability matrix carries its own provenance.

Diagnostics reach the harness on standard error, so a report that captures only standard output drops
exactly the thing the matrix is for.

The per-engine diagnostic snapshots, taken together, are the capability matrix. They are generated
documentation of what each engine cannot do, written by the tool rather than by hand.

## Coverage

Scenarios test what someone thought to write. The space of what *could* be tested is enumerable from the
model: `SchemaObjectKind` × `MemberKind` × `ChangeKind`.

Coverage is read off the plan each scenario produces — the diff nodes it emits are what it covers. No
tagging, nothing to keep in sync. The run then reports the cross product minus what was observed, which is
the list of scenarios not yet written.

The cross product is the floor, not the ceiling: it covers the diff grammar, and says nothing about the
variety inside a node — a column add is "covered" long before a column typed by an extension type has ever
been seen. Content variety is what the corpus is for, which is why its breadth matters more than its count.

Asserted, not described — the same discipline as the architecture rules.

## Reporting

A run that fails with a six-thousand line model diff teaches nothing. Two rules:

- **Every finding carries a stage and an object address.** The model's addresses make this close to free.
- **Findings group by symptom, not by case.** One introspection gap fires on forty tables across six
  schemas. That is one finding with forty occurrences and a representative, not forty failures. The
  difference between a report that reads as *217 failures* and one that reads as *4 bugs* is entirely in
  the grouping, and it decides whether anyone opens it on a Monday.

The published artifact is a matrix — case × engine, each cell pass, limitation, or fail — generated as
markdown for the docs site. Per-stage timings are recorded alongside it. An 800-table schema has things to
say about planner performance that fixtures cannot.

## Execution

- **xUnit v3**, theory over the matrix. Gives Testcontainers, Verify, and local `dotnet test` for free, and
  keeps the harness debuggable in an IDE rather than only in CI.
- **`GauntletRun` is the composition root**, and the assembly fixture — it owns the settings, the catalogs
  and the fleet, and everything below it takes what it needs as a constructor argument. Static is reserved
  for the one place the test framework forces it: naming the cells. xUnit resolves theory data during
  discovery, before any fixture exists and possibly in another process, so the matrix reads the settings
  itself. Nothing else does, and nothing static holds a resource.
- **Driven through the CLI**, not Core's API. It is the surface users touch, it exercises configuration,
  plugin loading, and state end to end, and it forces the machine-readable output and exit codes to be good
  enough to build on.
- **Everything a run is pinned to lives in `appsettings.json`** — image tags, provider package versions,
  and the CLI's own version. Deliberately the only source: no environment overrides, so what CI runs is
  what the file in git says it runs, and a run is reproducible from the commit alone. Changing what the
  matrix covers is a reviewed edit. The run installs the pinned CLI into a version-keyed tool path at
  startup, so there is no tool manifest resolving anything before the settings are read.
- **Each case gets a container and a generated project.** The `config.sql` pointing at the container is
  generated per run, never checked in. State is file state in the run's temporary directory.

### Cadence

|           | Runtime          | Runs                           |
|-----------|------------------|--------------------------------|
| Scenarios | seconds          | every PR, across the fleet     |
| Corpora   | minutes to hours | nightly, and as a release gate |

A scenario database is two tables. That difference in cost is why the two entry points stay distinct even
though they share an engine.

## Out of scope

- **Data fidelity.** Schemas only. Row seeds exist to trigger hazards, not to be verified.
- **Performance assertion.** Timings are recorded and reported, not gated.
- **`drift` and `doctor`.** The operations exercised are import, format, refresh, plan, and apply; the
  read-only conveniences have no gauntlet story yet, deliberately.

## Build order

1. **The transition engine, and `foreign-key-retarget` across all three engines.** Small, exercises the
   primitive the corpus also needs, and produces a red SQLite cell on day one — which is what proves the
   capability-snapshot design earns its keep.
2. **The report shape**, while the failures are still small enough to read.
3. **Fidelity, and Chinook.** Mostly reuse.
4. **The remaining engines' corpora**, then the coverage report, then the published matrix.

Corpus order after Chinook: Pagila (domains, enums, triggers, functions), AdventureWorks (SQL Server
breadth), Northwind, then something with a decade of scar tissue on it.
