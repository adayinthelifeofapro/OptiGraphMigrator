using System.IO;

namespace OptiGraphMigrator.Reporting
{
    /// <summary>Writes a human-readable, aligned table to the console.</summary>
    public sealed class ConsoleReportWriter : IReportWriter
    {
        /// <inheritdoc />
        public void Write(MigrationReport report, TextWriter writer)
        {
            WriteHeader(report, writer);

            if (report.Findings.Count == 0)
            {
                writer.WriteLine("No Search & Navigation (Find) usage detected. Nothing to migrate.");
                return;
            }

            foreach (var finding in report.Findings)
            {
                writer.WriteLine($"{finding.Severity.ToUpperInvariant(),-7} {finding.RuleId,-8} {finding.FilePath}({finding.StartLine},{finding.StartColumn}): {finding.Message}");

                if (!string.IsNullOrEmpty(finding.GraphEquivalent))
                {
                    writer.WriteLine($"        -> Graph equivalent: {finding.GraphEquivalent}");
                }

                if (!string.IsNullOrEmpty(finding.SuggestedApproach))
                {
                    writer.WriteLine($"        -> Suggested approach: {finding.SuggestedApproach}");
                }
            }

            writer.WriteLine();
            writer.WriteLine($"Summary: {report.ErrorCount} error(s), {report.WarningCount} warning(s), {report.InfoCount} info");
        }

        private static void WriteHeader(MigrationReport report, TextWriter writer)
        {
            if (string.IsNullOrEmpty(report.DetectedCmsVersion) && !report.IsHeuristic)
            {
                return;
            }

            if (!string.IsNullOrEmpty(report.DetectedCmsVersion))
            {
                writer.WriteLine($"Detected Optimizely CMS version: {report.DetectedCmsVersion}");
            }

            if (report.IsHeuristic)
            {
                writer.WriteLine("WARNING: one or more projects could not be resolved by MSBuild; results are heuristic (name-based matching) and may include false positives.");
            }

            writer.WriteLine();
        }
    }
}
