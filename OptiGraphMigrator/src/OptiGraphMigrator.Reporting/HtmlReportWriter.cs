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
            writer.WriteLine("body { font-family: Segoe UI, Arial, sans-serif; margin: 2rem; background: #12141c; color: #e6e8ef; }");
            writer.WriteLine(".panel { background: #171a24; border: 1px solid #2a2e3d; border-radius: 12px; padding: 1.5rem; margin-bottom: 1.5rem; }");
            writer.WriteLine(".panel-title { font-size: 0.85rem; letter-spacing: 0.08em; color: #8b90a3; margin: 0 0 1rem 0; text-transform: uppercase; }");
            writer.WriteLine(".summary-grid { display: flex; flex-wrap: wrap; gap: 1rem; }");
            writer.WriteLine(".summary-card { flex: 1 1 160px; background: #1d2130; border: 1px solid #2a2e3d; border-radius: 10px; padding: 1rem 1.25rem; }");
            writer.WriteLine(".summary-label { font-size: 0.75rem; color: #8b90a3; text-transform: uppercase; letter-spacing: 0.04em; margin-bottom: 0.5rem; }");
            writer.WriteLine(".summary-value { font-size: 2rem; font-weight: 600; }");
            writer.WriteLine(".summary-sub { font-size: 0.85rem; margin-left: 0.4rem; }");
            writer.WriteLine(".summary-value.error, .summary-sub.error { color: #ff5c5c; }");
            writer.WriteLine(".summary-sub.ok { color: #3ddc84; }");
            writer.WriteLine("table { border-collapse: collapse; width: 100%; margin-bottom: 1.5rem; }");
            writer.WriteLine("th, td { border-bottom: 1px solid #2a2e3d; padding: 0.6rem 0.75rem; text-align: left; vertical-align: top; }");
            writer.WriteLine("th { color: #8b90a3; font-size: 0.75rem; text-transform: uppercase; letter-spacing: 0.04em; font-weight: 600; }");
            writer.WriteLine("code, pre { background: #0d0f16; color: #cfd3e0; border-radius: 4px; padding: 0.1rem 0.35rem; }");
            writer.WriteLine("pre { padding: 0.6rem; overflow-x: auto; }");
            writer.WriteLine(".severity-error { color: #ff5c5c; font-weight: bold; }");
            writer.WriteLine(".severity-warning { color: #f0b02e; font-weight: bold; }");
            writer.WriteLine(".severity-info { color: #5aa9f8; }");
            writer.WriteLine(".equivalent-yes, .equivalent-no { display: inline-flex; align-items: center; justify-content: center; width: 1.6rem; height: 1.6rem; border-radius: 50%; font-size: 0.9rem; }");
            writer.WriteLine(".equivalent-yes { background: rgba(61, 220, 132, 0.15); color: #3ddc84; }");
            writer.WriteLine(".equivalent-no { background: rgba(255, 92, 92, 0.15); color: #ff5c5c; }");
            writer.WriteLine(".no-equivalent-text { color: #ff8080; }");
            writer.WriteLine(".suggested-approach { color: #b9c0d4; font-size: 0.9rem; line-height: 1.5; border-left: 3px solid #5aa9f8; background: #1a1e2b; }");
            writer.WriteLine(".suggested-approach-label { display: block; font-size: 0.7rem; text-transform: uppercase; letter-spacing: 0.06em; color: #5aa9f8; margin-bottom: 0.35rem; font-weight: 600; }");
            writer.WriteLine("</style>");
            writer.WriteLine("</head>");
            writer.WriteLine("<body>");
            writer.WriteLine("<h1>OptiGraphMigrator report</h1>");

            if (!string.IsNullOrEmpty(report.DetectedCmsVersion))
            {
                writer.WriteLine($"<p>Detected Optimizely CMS version: <strong>{WebUtility.HtmlEncode(report.DetectedCmsVersion)}</strong></p>");
            }

            if (report.IsHeuristic)
            {
                writer.WriteLine("<p class=\"no-equivalent-text\"><strong>Warning:</strong> one or more projects could not be resolved by MSBuild; results are heuristic (name-based matching) and may include false positives.</p>");
            }

            var mappedCount = report.ExactCount + report.CaveatCount;
            var totalCalls = report.Findings.Count;
            var noEquivalentCount = report.BlockedCount;
            var filesAffected = report.Findings.Select(f => f.FilePath).Distinct().Count();
            var mappedPercent = totalCalls == 0 ? 0 : mappedCount * 100.0 / totalCalls;
            var noEquivalentPercent = totalCalls == 0 ? 0 : noEquivalentCount * 100.0 / totalCalls;

            writer.WriteLine("<div class=\"panel\">");
            writer.WriteLine("<p class=\"panel-title\">Summary</p>");
            writer.WriteLine("<div class=\"summary-grid\">");
            writer.WriteLine("<div class=\"summary-card\"><div class=\"summary-label\">Total Calls Found</div>" +
                $"<div class=\"summary-value\">{totalCalls}</div></div>");
            writer.WriteLine("<div class=\"summary-card\"><div class=\"summary-label\">Mapped to Optimizely Graph</div>" +
                $"<div class=\"summary-value\">{mappedCount} <span class=\"summary-sub ok\">{mappedPercent:0.0}%</span></div></div>");
            writer.WriteLine("<div class=\"summary-card\"><div class=\"summary-label\">No Equivalent</div>" +
                $"<div class=\"summary-value error\">{noEquivalentCount} <span class=\"summary-sub error\">{noEquivalentPercent:0.0}%</span></div></div>");
            writer.WriteLine("<div class=\"summary-card\"><div class=\"summary-label\">Files Affected</div>" +
                $"<div class=\"summary-value\">{filesAffected}</div></div>");
            writer.WriteLine("</div>");
            writer.WriteLine("</div>");

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
                writer.WriteLine("<div class=\"panel\">");
                writer.WriteLine("<p class=\"panel-title\">Top blocking patterns</p>");
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
                writer.WriteLine("</div>");
            }

            writer.WriteLine("<div class=\"panel\">");
            writer.WriteLine("<table>");
            writer.WriteLine("<thead><tr><th>Location</th><th>Call</th><th>Optimizely Graph Equivalent</th><th>Equivalent</th></tr></thead>");
            writer.WriteLine("<tbody>");

            foreach (var finding in report.Findings)
            {
                var location = $"{finding.FilePath}:{finding.StartLine}";
                var hasEquivalent = !string.IsNullOrEmpty(finding.GraphEquivalent);
                var graphEquivalentText = hasEquivalent
                    ? $"<code>{Encode(finding.GraphEquivalent!)}</code>"
                    : "<span class=\"no-equivalent-text\">— NO EQUIVALENT —</span>";
                var equivalentIcon = hasEquivalent
                    ? "<span class=\"equivalent-yes\" title=\"Mapped\">&#10004;</span>"
                    : "<span class=\"equivalent-no\" title=\"No equivalent\">&#9888;</span>";
                writer.WriteLine($"<tr><td>{Encode(location)}</td><td><code>{Encode(finding.Message)}</code></td><td>{graphEquivalentText}</td><td>{equivalentIcon}</td></tr>");

                if (!string.IsNullOrEmpty(finding.GraphQlSnippet))
                {
                    writer.WriteLine($"<tr><td colspan=\"4\"><pre>{Encode(finding.GraphQlSnippet!)}</pre></td></tr>");
                }

                if (!string.IsNullOrEmpty(finding.SuggestedApproach))
                {
                    writer.WriteLine(
                        "<tr><td colspan=\"4\" class=\"suggested-approach\">" +
                        $"<span class=\"suggested-approach-label\">Suggested approach</span>{Encode(finding.SuggestedApproach!)}</td></tr>");
                }
            }

            writer.WriteLine("</tbody>");
            writer.WriteLine("</table>");
            writer.WriteLine("</div>");
            writer.WriteLine("</body>");
            writer.WriteLine("</html>");
        }

        private static string Encode(string value) => WebUtility.HtmlEncode(value) ?? string.Empty;
    }
}
