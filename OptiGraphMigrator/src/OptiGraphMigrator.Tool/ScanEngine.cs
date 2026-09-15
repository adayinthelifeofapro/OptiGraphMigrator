using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.MSBuild;
using OptiGraphMigrator.Analyzers;
using OptiGraphMigrator.Core.Rules;
using OptiGraphMigrator.Reporting;

namespace OptiGraphMigrator.Tool
{
    /// <summary>
    /// Loads a solution or project with <see cref="MSBuildWorkspace"/> and runs the
    /// OptiGraphMigrator analyzers over every C# project it contains, producing a
    /// format-agnostic <see cref="MigrationReport"/>.
    /// </summary>
    internal static class ScanEngine
    {
        private static readonly ImmutableArray<DiagnosticAnalyzer> Analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(
            new FindUsageAnalyzer(),
            new FindConfigurationAnalyzer(),
            new FindContentLoaderAnalyzer());

        public static async Task<MigrationReport> ScanAsync(ScanOptions options, TextWriter errorWriter)
        {
            using var workspace = MSBuildWorkspace.Create();
            workspace.WorkspaceFailed += (sender, e) =>
            {
                if (e.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
                {
                    errorWriter.WriteLine($"warning: {e.Diagnostic.Message}");
                }
            };

            var projects = await LoadProjectsAsync(workspace, options.Path).ConfigureAwait(false);

            var usedSourceOnlyFallback = false;
            if (!projects.Any(p => p.Language == LanguageNames.CSharp))
            {
                var fallbackProjects = SourceOnlyProjectLoader.Load(options.Path);
                if (fallbackProjects.Count > 0)
                {
                    errorWriter.WriteLine(
                        "warning: MSBuild could not load any C# project from the scan target; falling back to a source-only heuristic scan. EPiServer.Find references could not be resolved, so results are name-based and may include false positives.");
                    projects = fallbackProjects;
                    usedSourceOnlyFallback = true;
                }
            }

            var findings = new List<MigrationFinding>();
            var solutionRoot = Path.GetDirectoryName(Path.GetFullPath(options.Path)) ?? string.Empty;
            var isHeuristic = usedSourceOnlyFallback;

            foreach (var project in projects)
            {
                if (project.Language != LanguageNames.CSharp)
                {
                    continue;
                }

                var compilation = await project.GetCompilationAsync().ConfigureAwait(false);
                if (compilation is null)
                {
                    continue;
                }

                var compilationWithAnalyzers = compilation.WithAnalyzers(Analyzers);
                var diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);

                foreach (var diagnostic in diagnostics)
                {
                    if (diagnostic.Properties.TryGetValue("Confidence", out var confidence) &&
                        string.Equals(confidence, "Heuristic", StringComparison.Ordinal))
                    {
                        isHeuristic = true;
                    }

                    var finding = ToFinding(diagnostic, solutionRoot);
                    if (finding is not null && SeverityLevel.Meets(finding.Severity, options.SeverityThreshold))
                    {
                        findings.Add(finding);
                    }
                }
            }

            foreach (var webConfigFinding in WebConfigScanner.Scan(solutionRoot))
            {
                if (SeverityLevel.Meets(webConfigFinding.Severity, options.SeverityThreshold))
                {
                    findings.Add(webConfigFinding);
                }
            }

            var detectedCmsVersion = ProjectFileInspector.Inspect(options.Path).DetectedCmsVersion;

            return new MigrationReport(
                findings
                    .GroupBy(f => (f.FilePath, f.StartLine, f.RuleId, f.Message), FindingKeyComparer.Instance)
                    .Select(g => g.First())
                    .OrderBy(f => f.FilePath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(f => f.StartLine)
                    .ToList(),
                isHeuristic,
                detectedCmsVersion);
        }

        /// <summary>
        /// Compares finding dedup keys at line granularity (ignoring column) and the file
        /// path's casing, so that findings collapse into a single entry when they are: (a)
        /// produced for the same physical file across multiple target frameworks or other
        /// duplicate project instances in the workspace, or (b) multiple distinct segments on
        /// the same source line that raise the same rule with the same message - which the
        /// report only ever surfaces at line-level granularity anyway, so presenting them
        /// separately would just look like unexplained duplicate rows.
        /// </summary>
        private sealed class FindingKeyComparer : IEqualityComparer<(string FilePath, int StartLine, string RuleId, string Message)>
        {
            public static readonly FindingKeyComparer Instance = new();

            public bool Equals((string FilePath, int StartLine, string RuleId, string Message) x, (string FilePath, int StartLine, string RuleId, string Message) y) =>
                string.Equals(x.FilePath, y.FilePath, StringComparison.OrdinalIgnoreCase) &&
                x.StartLine == y.StartLine &&
                string.Equals(x.RuleId, y.RuleId, StringComparison.Ordinal) &&
                string.Equals(x.Message, y.Message, StringComparison.Ordinal);

            public int GetHashCode((string FilePath, int StartLine, string RuleId, string Message) obj)
            {
                var hash = new HashCode();
                hash.Add(obj.FilePath, StringComparer.OrdinalIgnoreCase);
                hash.Add(obj.StartLine);
                hash.Add(obj.RuleId, StringComparer.Ordinal);
                hash.Add(obj.Message, StringComparer.Ordinal);
                return hash.ToHashCode();
            }
        }

        private static async Task<IReadOnlyList<Project>> LoadProjectsAsync(MSBuildWorkspace workspace, string path)
        {
            var extension = Path.GetExtension(path);

            if (string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase))
            {
                var solution = await workspace.OpenSolutionAsync(path).ConfigureAwait(false);
                return solution.Projects.ToList();
            }

            var project = await workspace.OpenProjectAsync(path).ConfigureAwait(false);
            return new[] { project };
        }

        private static MigrationFinding? ToFinding(Diagnostic diagnostic, string solutionRoot)
        {
            var lineSpan = diagnostic.Location.GetLineSpan();
            var filePath = lineSpan.Path;
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            var relativePath = MakeRelative(solutionRoot, filePath);

            diagnostic.Properties.TryGetValue("GraphEquivalent", out var graphEquivalent);
            diagnostic.Properties.TryGetValue("GraphQlSnippet", out var graphQlSnippet);
            diagnostic.Properties.TryGetValue("Translatability", out var translatability);
            diagnostic.Properties.TryGetValue("SuggestedApproach", out var suggestedApproach);

            var (title, docsUrl) = ResolveRuleMetadata(diagnostic.Id);

            return new MigrationFinding(
                diagnostic.Id,
                ToSeverity(diagnostic.Severity),
                diagnostic.GetMessage(),
                relativePath,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1,
                lineSpan.EndLinePosition.Line + 1,
                lineSpan.EndLinePosition.Character + 1,
                string.IsNullOrEmpty(graphEquivalent) ? null : graphEquivalent,
                string.IsNullOrEmpty(graphQlSnippet) ? null : graphQlSnippet,
                title,
                string.IsNullOrEmpty(translatability) ? null : translatability!.ToLowerInvariant(),
                docsUrl,
                string.IsNullOrEmpty(suggestedApproach) ? null : suggestedApproach);
        }

        private static (string? Title, string? DocsUrl) ResolveRuleMetadata(string ruleId)
        {
            var declarativeRule = RuleCatalogueLoader.Default.GetById(ruleId);
            if (declarativeRule is not null)
            {
                return (declarativeRule.Title, declarativeRule.GetDocsUrl());
            }

            var complexRule = ComplexRuleRegistry.All.FirstOrDefault(r => r.Id == ruleId);
            return complexRule is not null ? (complexRule.Title, (string?)null) : (null, null);
        }

        private static string MakeRelative(string root, string filePath)
        {
            if (string.IsNullOrEmpty(root))
            {
                return filePath;
            }

            try
            {
                var rootUri = new Uri(root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar);
                var fileUri = new Uri(filePath);
                return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
            catch (UriFormatException)
            {
                return filePath;
            }
        }

        private static string ToSeverity(DiagnosticSeverity severity) => severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Warning => "warning",
            _ => "info"
        };
    }
}
