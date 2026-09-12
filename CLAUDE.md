# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

The git root (`C:\Development\Playroom\OptiGraphMigrator`) contains a single nested folder,
`OptiGraphMigrator\`, which holds the actual .NET solution (`OptiGraphMigrator.slnx`). All
commands below assume you are working from that inner `OptiGraphMigrator\` directory.

## What this project is

A Roslyn analyzer, code fix provider, and `dotnet` CLI tool (`optigraph-migrate`) that scans a
.NET solution for Optimizely Search & Navigation ("Find") usage, maps each call to its
Optimizely Graph SDK equivalent, and flags patterns that have no clean translation. See
`OptiGraphMigrator\README.md` for the full CLI option reference and exit code semantics.

## Common commands

```powershell
# Build / test everything
dotnet build OptiGraphMigrator.slnx
dotnet test OptiGraphMigrator.slnx

# Run a single test project
dotnet test tests\OptiGraphMigrator.Core.Tests\OptiGraphMigrator.Core.Tests.csproj

# Run a single test by fully-qualified name
dotnet test tests\OptiGraphMigrator.Analyzers.Tests\OptiGraphMigrator.Analyzers.Tests.csproj --filter "FullyQualifiedName~FindUsageAnalyzerTests.SomeTest"

# Run the CLI from source against the bundled sample
dotnet run --project src\OptiGraphMigrator.Tool\OptiGraphMigrator.Tool.csproj -- scan samples\SampleFindSolution\SampleFindSolution.csproj --format console

# Pack the tool and install it locally as `optigraph-migrate`
dotnet pack src\OptiGraphMigrator.Tool\OptiGraphMigrator.Tool.csproj -c Release
dotnet tool install --global --add-source artifacts OptiGraphMigrator.Tool
```

`OptiGraphMigrator.Tool.Tests` targets `net10.0` and needs a matching installed SDK/MSBuild
(it loads real projects via `MSBuildWorkspace`); the other test projects target `netstandard2.0`
libraries and run under any recent SDK.

## Architecture

The system is a pipeline: **Roslyn syntax → semantic chain → rule match → diagnostic/report**,
implemented across five projects with a strict dependency direction
(`Core` ← `Analyzers`/`Reporting` ← `CodeFixes`/`Tool`):

1. **`OptiGraphMigrator.Core`** (`netstandard2.0`) — no Roslyn-analyzer dependency, just the
   rule engine and chain model, so it can be shared by the analyzer, the CLI, and tests alike.
   - `Analysis/FindChainWalker.cs` finds every Find fluent-query invocation
     (`IClient.Search<T>()...GetContentResult()`), climbs to the outermost invocation of the
     chain, and walks back down through receivers to collect every segment (`FindChain` /
     `FindChainSegment`) in source order. It also follows simple local-variable reassignment
     (`query = query.Foo()`) so a chain split across statements still resolves as one chain.
   - `Rules/MigrationRuleEngine.cs` evaluates each `FindChainSegment` against two rule sources,
     **complex rules first**: hand-written `IMigrationRule` implementations in `Rules/Complex/`
     handle patterns the declarative catalogue can't express (and can override/narrow a
     declarative "exact" mapping — e.g. `InMemoryPredicateFilterRule` overrides `OGM001` for
     `Filter()` predicates that call untranslatable arbitrary code). If no complex rule matches,
     it falls back to the declarative catalogue (`Rules/RuleCatalogueLoader.cs`), which loads
     `Resources/find-to-graph.rules.json` (embedded resource) — a table of `containingType` +
     `methodName` + arg-count patterns mapped to a Graph equivalent, an optional GraphQL snippet
     template, severity, and caveats. Users can extend/override this catalogue with an
     `optigraph.rules.json` file (same schema) discovered by walking up from the scanned path,
     the same way `.editorconfig` is discovered.
   - Adding a new Find→Graph mapping usually means adding one entry to
     `find-to-graph.rules.json`; only add a new `Rules/Complex/*Rule.cs` class when the pattern
     needs semantic inspection the declarative shape can't express (as most existing `Complex`
     rules do — inspecting lambda bodies, symbol containment, etc.).

2. **`OptiGraphMigrator.Analyzers`** (`netstandard2.0`) — `DiagnosticAnalyzer`s
   (`FindUsageAnalyzer`, `FindConfigurationAnalyzer`, `FindContentLoaderAnalyzer`,
   `FindIndexingAnalyzer`) that drive `FindChainWalker` + `MigrationRuleEngine` per compilation
   and report one diagnostic per matched segment, carrying the rule's Graph equivalent/snippet
   as diagnostic properties. Every rule ID (declarative and complex) needs a
   `DiagnosticDescriptor`, built dynamically in `DiagnosticDescriptors.cs`; new rule IDs should
   also get an entry in `AnalyzerReleases.Unshipped.md` per Roslyn analyzer release-tracking
   convention.

3. **`OptiGraphMigrator.CodeFixes`** (`netstandard2.0`) — `CodeFixProvider`s (currently
   `FindChainCodeFixProvider`) for the subset of rules marked `isAutoFixable` in the catalogue.

4. **`OptiGraphMigrator.Reporting`** — format-agnostic `MigrationReport`/`MigrationFinding`
   models plus one `IReportWriter` per output format (console, JSON, SARIF, Markdown). Golden
   files for each format live in `tests/OptiGraphMigrator.Reporting.Tests/GoldenFiles/`; update
   those fixtures when intentionally changing a writer's output.

5. **`OptiGraphMigrator.Tool`** (`net10.0`, packed as the `optigraph-migrate` dotnet tool) —
   `CommandLineApp.cs` parses CLI args (`System.CommandLine`) and `ScanEngine.cs` loads the
   target solution/project via `MSBuildWorkspace`, runs all analyzers over every C# project via
   `Compilation.WithAnalyzers`, converts diagnostics to `MigrationFinding`s, and hands them to
   the chosen `IReportWriter`. Exit code is 0/1/2 depending on whether any finding met
   `--fail-on` or the scan itself failed to run — see the README's exit-code table before
   changing this logic, since CI pipelines gate on it.

`samples/EPiServer.Find.Stubs` and `samples/SampleFindSolution` are a self-contained stand-in
for the real `EPiServer.Find` SDK (so the repo doesn't need that private NuGet feed) plus a
sample project with representative Find usage; both are referenced by
`OptiGraphMigrator.Tool.Tests` for CLI integration tests and are the target for manual
`dotnet run ... scan` testing.

## Conventions

- `Directory.Packages.props` centrally manages package versions (`ManagePackageVersionsCentrally`)
  — add new dependencies there, not with inline `Version=` attributes in a `.csproj`.
- `Directory.Build.props` sets solution-wide defaults: `Nullable` enabled, `ImplicitUsings`
  disabled (so files need explicit `using` directives), warnings-as-errors is off but
  `CA1062`/`CA1848`/`CA2007`/`NU5128` are suppressed globally.
- Packing `OptiGraphMigrator.Analyzers` bundles `OptiGraphMigrator.Core` and
  `OptiGraphMigrator.CodeFixes` DLLs into `analyzers/dotnet/cs/` via a custom MSBuild target
  (`_OptiGraphMigrator_PackAnalyzerAssemblies` in that project's `.csproj`); `CodeFixes` must be
  built before packing `Analyzers` or that step silently omits the code-fix DLL.
