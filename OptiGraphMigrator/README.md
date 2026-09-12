# OptiGraphMigrator

Roslyn analyzer, code fix provider, and `dotnet` CLI tool that scans a solution for
Optimizely Search &amp; Navigation ("Find") usage, maps each call to its Optimizely Graph
SDK equivalent, and flags patterns that have no clean translation.

## Solution structure

| Project | Description |
|---|---|
| `src/OptiGraphMigrator.Core` | Shared rule catalogue and Find-to-Graph mapping metadata (`netstandard2.0`). |
| `src/OptiGraphMigrator.Analyzers` | Roslyn analyzers that detect Find usage (`netstandard2.0`). |
| `src/OptiGraphMigrator.CodeFixes` | Roslyn code fix providers for auto-fixable findings (`netstandard2.0`). |
| `src/OptiGraphMigrator.Reporting` | Report models and writers (console, JSON, SARIF, Markdown). |
| `src/OptiGraphMigrator.Tool` | The `optigraph-migrate` CLI (`net8.0`), packaged as a `dotnet tool`. |
| `tests/OptiGraphMigrator.Core.Tests` | Unit tests for the rule catalogue. |
| `tests/OptiGraphMigrator.Analyzers.Tests` | Analyzer/code fix unit tests. |
| `tests/OptiGraphMigrator.Reporting.Tests` | Golden-file tests for each report writer/format. |
| `tests/OptiGraphMigrator.Tool.Tests` | CLI integration tests (`net10.0`, requires an installed MSBuild/SDK). |
| `samples/EPiServer.Find.Stubs` | Minimal stand-in for the EPiServer.Find SDK, used to build a realistic sample. |
| `samples/SampleFindSolution` | Sample project with representative Find usage, used for manual/integration testing. |

## Building and testing

```powershell
dotnet build OptiGraphMigrator.slnx
dotnet test OptiGraphMigrator.slnx
```

## Running the CLI

### From source

```powershell
dotnet run --project src\OptiGraphMigrator.Tool\OptiGraphMigrator.Tool.csproj -- scan <path> [options]
```

For example, against the sample solution included in this repo:

```powershell
dotnet run --project src\OptiGraphMigrator.Tool\OptiGraphMigrator.Tool.csproj -- scan samples\SampleFindSolution\SampleFindSolution.csproj --format console
```

### As an installed .NET tool

The tool project is configured with `PackAsTool` (command name `optigraph-migrate`). To
build and install it locally:

```powershell
dotnet pack src\OptiGraphMigrator.Tool\OptiGraphMigrator.Tool.csproj -c Release
dotnet tool install --global --add-source artifacts OptiGraphMigrator.Tool
```

Once installed, run it directly:

```powershell
optigraph-migrate scan <path> [options]
```

## CLI usage

`<path>` is a solution (`.sln`/`.slnx`) or project file to scan.

| Option | Description |
|---|---|
| `--output, -o <file>` | Write the report to a file instead of stdout. |
| `--format <console\|sarif\|json\|markdown>` | Report format. Defaults to `console`. |
| `--rules <file>` | Path to a rule catalogue overriding/extending the built-in Find-to-Graph mappings. |
| `--severity-threshold <info\|warning\|error>` | Minimum severity a finding must have to be included in the report. Defaults to `info`. |
| `--fail-on <info\|warning\|error>` | Minimum severity that causes a non-zero exit code. Defaults to `error`. |

### Exit codes

CI pipelines should gate on the process exit code:

| Code | Meaning |
|---|---|
| `0` | Scan completed and no finding met the `--fail-on` threshold. |
| `1` | Scan completed but at least one finding met or exceeded the `--fail-on` threshold. |
| `2` | The scan could not run at all (missing/invalid path, workspace load failure, or an unexpected error). |

For example, in a CI job that should only break the build on unresolved `Blocked`
patterns, run with `--fail-on error` (the default) and treat exit code `1` as a
required, but non-blocking, informational failure, or `2` as an infrastructure problem
that should always fail the job.
