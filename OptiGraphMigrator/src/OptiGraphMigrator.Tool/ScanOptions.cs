using OptiGraphMigrator.Reporting;

namespace OptiGraphMigrator.Tool
{
    /// <summary>Resolved options for a <c>scan</c> invocation.</summary>
    public sealed class ScanOptions
    {
        /// <summary>Path to the solution (.sln/.slnx) or project file to scan.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>Optional output file path. When null, the report is written to stdout.</summary>
        public string? Output { get; set; }

        /// <summary>Report format.</summary>
        public ReportFormat Format { get; set; } = ReportFormat.Console;

        /// <summary>Optional path to a user-supplied rule catalogue overriding/extending the built-in one.</summary>
        public string? RulesPath { get; set; }

        /// <summary>Minimum severity a finding must have to be included in the report.</summary>
        public string SeverityThreshold { get; set; } = "info";

        /// <summary>Minimum severity that causes the process to exit with a non-zero code.</summary>
        public string FailOn { get; set; } = "error";
    }
}
