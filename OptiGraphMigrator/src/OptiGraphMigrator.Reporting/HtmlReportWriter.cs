using System.IO;
using System.Linq;
using System.Net;

namespace OptiGraphMigrator.Reporting
{
    /// <summary>Writes findings as a self-contained HTML report, suitable for viewing in a browser or attaching to a build.</summary>
    public sealed class HtmlReportWriter : IReportWriter
    {
        /// <inheritdoc />
        public void Write(MigrationReport report, TextWriter writer)
        {
            writer.WriteLine("<!DOCTYPE html>");
            writer.WriteLine("<html lang=\"en\">");
            writer.WriteLine("<head>");
            writer.WriteLine("<meta charset=\"utf-8\" />");
            writer.WriteLine("<title>OptiGraphMigrator report</title>");
            writer.WriteLine("<style>");
            writer.WriteLine("body { font-family: Segoe UI, Arial, sans-serif; margin: 2rem; color: #222; }");
            writer.WriteLine("table { border-collapse: collapse; width: 100%; margin-bottom: 1.5rem; }");
            writer.WriteLine("th, td { border: 1px solid #ccc; padding: 0.4rem 0.6rem; text-align: left; vertical-align: top; }");
            writer.WriteLine("th { background: #f2f2f2; }");
            writer.WriteLine("code, pre { background: #f5f5f5; }");
            writer.WriteLine(".severity-error { color: #b00020; font-weight: bold; }");
            writer.WriteLine(".severity-warning { color: #9a6700; font-weight: bold; }");
            writer.WriteLine(".severity-info { color: #0b6ed0; }");
            writer.WriteLine("</style>");
            writer.WriteLine("</head>");
            writer.WriteLine("<body>");
            writer.WriteLine("<h1>OptiGraphMigrator report</h1>");
            writer.WriteLine($"<p>{report.ErrorCount} error(s), {report.WarningCount} warning(s), {report.InfoCount} info</p>");
            writer.WriteLine("<ul>");
            writer.WriteLine($"<li>Exact (auto-fixable): {report.ExactCount}</li>");
            writer.WriteLine($"<li>Caveat (needs review): {report.CaveatCount}</li>");
            writer.WriteLine($"<li>Blocked (no clean translation): {report.BlockedCount}</li>");
            writer.WriteLine("</ul>");

            if (report.Findings.Count == 0)
            {
                writer.WriteLine("<p>No Search &amp; Navigation (Find) usage detected. Nothing to migrate.</p>");
                writer.WriteLine("</body>");
                writer.WriteLine("</html>");
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
                writer.WriteLine("<h2>Top blocking patterns</h2>");
                writer.WriteLine("<table>");
                writer.WriteLine("<thead><tr><th>Rule</th><th>Title</th><th>Occurrences</th></tr></thead>");
                writer.WriteLine("<tbody>");

                foreach (var group in topBlocking)
                {
                    var title = group.First().Title ?? string.Empty;
                    writer.WriteLine($"<tr><td>{Encode(group.Key)}</td><td>{Encode(title)}</td><td>{group.Count()}</td></tr>");
                }

                writer.WriteLine("</tbody>");
                writer.WriteLine("</table>");
            }

            writer.WriteLine("<h2>Findings</h2>");
            writer.WriteLine("<table>");
            writer.WriteLine("<thead><tr><th>Severity</th><th>Rule</th><th>Location</th><th>Message</th><th>Graph equivalent</th></tr></thead>");
            writer.WriteLine("<tbody>");

            foreach (var finding in report.Findings)
            {
                var location = $"{finding.FilePath}({finding.StartLine},{finding.StartColumn})";
                var graphEquivalent = string.IsNullOrEmpty(finding.GraphEquivalent) ? string.Empty : $"<code>{Encode(finding.GraphEquivalent!)}</code>";
                var severityClass = $"severity-{finding.Severity.ToLowerInvariant()}";
                writer.WriteLine($"<tr><td class=\"{severityClass}\">{Encode(finding.Severity)}</td><td>{Encode(finding.RuleId)}</td><td>{Encode(location)}</td><td>{Encode(finding.Message)}</td><td>{graphEquivalent}</td></tr>");

                if (!string.IsNullOrEmpty(finding.GraphQlSnippet))
                {
                    writer.WriteLine($"<tr><td colspan=\"5\"><pre>{Encode(finding.GraphQlSnippet!)}</pre></td></tr>");
                }
            }

            writer.WriteLine("</tbody>");
            writer.WriteLine("</table>");
            writer.WriteLine("</body>");
            writer.WriteLine("</html>");
        }

        private static string Encode(string value) => WebUtility.HtmlEncode(value) ?? string.Empty;
    }
}
