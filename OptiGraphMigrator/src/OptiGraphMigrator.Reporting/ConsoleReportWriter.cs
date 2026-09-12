using System.IO;

namespace OptiGraphMigrator.Reporting
{
    /// <summary>Writes a human-readable, aligned table to the console.</summary>
    public sealed class ConsoleReportWriter : IReportWriter
    {
        /// <inheritdoc />
        public void Write(MigrationReport report, TextWriter writer)
        {
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
            }

            writer.WriteLine();
            writer.WriteLine($"Summary: {report.ErrorCount} error(s), {report.WarningCount} warning(s), {report.InfoCount} info");
        }
    }
}
