namespace OptiGraphMigrator.Reporting
{
    /// <summary>The output formats supported by <c>optigraph-migrate scan</c>.</summary>
    public enum ReportFormat
    {
        /// <summary>Human-readable console table (also the default when no <c>--output</c> is given).</summary>
        Console,

        /// <summary>SARIF 2.1.0, for CI annotation / GitHub code scanning integration.</summary>
        Sarif,

        /// <summary>Plain JSON array of findings.</summary>
        Json,

        /// <summary>Markdown table, suitable for PR summaries.</summary>
        Markdown,

        /// <summary>Self-contained HTML report, suitable for viewing in a browser.</summary>
        Html
    }

    /// <summary>Parses a <c>--format</c> value into a <see cref="ReportFormat"/>.</summary>
    public static class ReportFormatParser
    {
        /// <summary>Parses <paramref name="value"/>, defaulting to <see cref="ReportFormat.Console"/> when null or empty.</summary>
        public static ReportFormat Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ReportFormat.Console;
            }

            return value!.Trim().ToLowerInvariant() switch
            {
                "console" => ReportFormat.Console,
                "sarif" => ReportFormat.Sarif,
                "json" => ReportFormat.Json,
                "markdown" or "md" => ReportFormat.Markdown,
                "html" or "htm" => ReportFormat.Html,
                _ => throw new System.ArgumentException($"Unknown report format '{value}'. Expected one of: console, sarif, json, markdown, html.")
            };
        }
    }
}
