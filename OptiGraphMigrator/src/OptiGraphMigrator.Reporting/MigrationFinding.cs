using System.Collections.Generic;

namespace OptiGraphMigrator.Reporting
{
    /// <summary>
    /// A single reported Find-to-Graph migration diagnostic, normalised from a Roslyn
    /// <see cref="Microsoft.CodeAnalysis.Diagnostic"/> into a format-agnostic shape the
    /// report writers can consume.
    /// </summary>
    public sealed class MigrationFinding
    {
        /// <summary>Creates a finding.</summary>
        public MigrationFinding(
            string ruleId,
            string severity,
            string message,
            string filePath,
            int startLine,
            int startColumn,
            int endLine,
            int endColumn,
            string? graphEquivalent,
            string? graphQlSnippet,
            string? title = null,
            string? translatability = null,
            string? docsUrl = null)
        {
            RuleId = ruleId;
            Severity = severity;
            Message = message;
            FilePath = filePath;
            StartLine = startLine;
            StartColumn = startColumn;
            EndLine = endLine;
            EndColumn = endColumn;
            GraphEquivalent = graphEquivalent;
            GraphQlSnippet = graphQlSnippet;
            Title = title;
            Translatability = translatability;
            DocsUrl = docsUrl;
        }

        /// <summary>Stable diagnostic id, for example <c>OGM001</c>.</summary>
        public string RuleId { get; }

        /// <summary>Reported severity: <c>error</c>, <c>warning</c>, or <c>info</c>.</summary>
        public string Severity { get; }

        /// <summary>Rendered diagnostic message.</summary>
        public string Message { get; }

        /// <summary>Path of the source file the finding was reported in, relative to the scan root when possible.</summary>
        public string FilePath { get; }

        /// <summary>1-based start line.</summary>
        public int StartLine { get; }

        /// <summary>1-based start column.</summary>
        public int StartColumn { get; }

        /// <summary>1-based end line.</summary>
        public int EndLine { get; }

        /// <summary>1-based end column.</summary>
        public int EndColumn { get; }

        /// <summary>The Optimizely Graph SDK equivalent, when one exists.</summary>
        public string? GraphEquivalent { get; }

        /// <summary>An illustrative GraphQL snippet contributed by this finding, when one exists.</summary>
        public string? GraphQlSnippet { get; }

        /// <summary>Short human readable rule title, when known.</summary>
        public string? Title { get; }

        /// <summary>How cleanly the construct maps onto Graph: exact, caveat, blocked, or unknown.</summary>
        public string? Translatability { get; }

        /// <summary>Link to the rule's documentation page, when known.</summary>
        public string? DocsUrl { get; }
    }

    /// <summary>The complete set of findings produced by a scan, plus summary counts.</summary>
    public sealed class MigrationReport
    {
        /// <summary>Creates a report.</summary>
        public MigrationReport(IReadOnlyList<MigrationFinding> findings)
        {
            Findings = findings;
        }

        /// <summary>All findings in the order they were reported.</summary>
        public IReadOnlyList<MigrationFinding> Findings { get; }

        /// <summary>Number of findings at error severity.</summary>
        public int ErrorCount => Count("error");

        /// <summary>Number of findings at warning severity.</summary>
        public int WarningCount => Count("warning");

        /// <summary>Number of findings at info severity.</summary>
        public int InfoCount => Count("info");

        /// <summary>Number of findings whose rule is an exact (auto-fixable) mapping.</summary>
        public int ExactCount => CountByTranslatability("exact");

        /// <summary>Number of findings whose rule maps with behavioural caveats.</summary>
        public int CaveatCount => CountByTranslatability("caveat");

        /// <summary>Number of findings whose rule has no clean Graph translation.</summary>
        public int BlockedCount => CountByTranslatability("blocked");

        private int Count(string severity)
        {
            var count = 0;
            foreach (var finding in Findings)
            {
                if (finding.Severity == severity)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountByTranslatability(string translatability)
        {
            var count = 0;
            foreach (var finding in Findings)
            {
                if (string.Equals(finding.Translatability, translatability, System.StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
