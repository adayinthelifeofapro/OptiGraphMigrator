using System.IO;
using System.Text.Json;

namespace OptiGraphMigrator.Reporting
{
    /// <summary>Writes findings as a plain JSON array, suitable for custom tooling to consume.</summary>
    public sealed class JsonReportWriter : IReportWriter
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        /// <inheritdoc />
        public void Write(MigrationReport report, TextWriter writer)
        {
            var json = JsonSerializer.Serialize(report.Findings, Options);
            writer.Write(json);
            writer.WriteLine();
        }
    }
}
