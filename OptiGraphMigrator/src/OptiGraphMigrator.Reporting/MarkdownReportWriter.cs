using System.IO;
using System.Linq;

namespace OptiGraphMigrator.Reporting
{
    /// <summary>Writes findings as a Markdown table, suitable for embedding in a pull request summary.</summary>
    public sealed class MarkdownReportWriter : IReportWriter
    {
        /// <inheritdoc />
        public void Write(MigrationReport report, TextWriter writer)
        {
            writer.WriteLine($"# OptiGraphMigrator report");
            writer.WriteLine();
            writer.WriteLine($"{report.ErrorCount} error(s), {report.WarningCount} warning(s), {report.InfoCount} info");
            writer.WriteLine();
            writer.WriteLine($"- Exact (auto-fixable): {report.ExactCount}");
            writer.WriteLine($"- Caveat (needs review): {report.CaveatCount}");
            writer.WriteLine($"- Blocked (no clean translation): {report.BlockedCount}");
            writer.WriteLine();

            if (report.Findings.Count == 0)
            {
                writer.WriteLine("No Search & Navigation (Find) usage detected. Nothing to migrate.");
                return;
            }

            var topBlocking = report.Findings
                .Where(f => string.Equals(f.Translatability, "blocked", System.StringComparison.OrdinalIgnoreCase))
                .GroupBy(f => f.RuleId)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .ToList();

            if (topBlocking.Count > 0)
            {
                writer.WriteLine("## Top blocking patterns");
                writer.WriteLine();
                writer.WriteLine("| Rule | Title | Occurrences |");
                writer.WriteLine("|---|---|---|");

                foreach (var group in topBlocking)
                {
                    var title = group.First().Title ?? string.Empty;
                    writer.WriteLine($"| {group.Key} | {title} | {group.Count()} |");
                }

                writer.WriteLine();

                writer.WriteLine("### Suggested approaches");
                writer.WriteLine();
                writer.WriteLine("These patterns have no Optimizely Graph equivalent, so the behaviour has to be re-implemented. Suggested starting points:");
                writer.WriteLine();

                foreach (var group in topBlocking)
                {
                    var suggestedApproach = group.Select(f => f.SuggestedApproach).FirstOrDefault(s => !string.IsNullOrEmpty(s));
                    if (!string.IsNullOrEmpty(suggestedApproach))
                    {
                        writer.WriteLine($"- **{group.Key}**: {suggestedApproach}");
                    }
                }

                writer.WriteLine();
            }

            writer.WriteLine("## Findings");
            writer.WriteLine();
            writer.WriteLine("| Severity | Rule | Location | Message | Graph equivalent |");
            writer.WriteLine("|---|---|---|---|---|");

            foreach (var finding in report.Findings)
            {
                var location = $"{finding.FilePath}({finding.StartLine},{finding.StartColumn})";
                var graphEquivalent = string.IsNullOrEmpty(finding.GraphEquivalent) ? string.Empty : $"`{finding.GraphEquivalent}`";
                writer.WriteLine($"| {finding.Severity} | {finding.RuleId} | {location} | {finding.Message} | {graphEquivalent} |");

                if (!string.IsNullOrEmpty(finding.GraphQlSnippet))
                {
                    writer.WriteLine();
                    writer.WriteLine("  ```graphql");
                    writer.WriteLine($"  {finding.GraphQlSnippet}");
                    writer.WriteLine("  ```");
                    writer.WriteLine();
                }

                if (!string.IsNullOrEmpty(finding.SuggestedApproach))
                {
                    writer.WriteLine();
                    writer.WriteLine($"> **Suggested approach:** {finding.SuggestedApproach}");
                    writer.WriteLine();
                }
            }
        }
    }
}
