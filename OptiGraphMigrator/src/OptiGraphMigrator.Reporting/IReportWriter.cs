using System.IO;

namespace OptiGraphMigrator.Reporting
{
    /// <summary>Writes a <see cref="MigrationReport"/> to a text destination in a specific format.</summary>
    public interface IReportWriter
    {
        /// <summary>Writes <paramref name="report"/> to <paramref name="writer"/>.</summary>
        void Write(MigrationReport report, TextWriter writer);
    }

    /// <summary>Resolves the <see cref="IReportWriter"/> for a given <see cref="ReportFormat"/>.</summary>
    public static class ReportWriterFactory
    {
        /// <summary>Creates the writer for <paramref name="format"/>.</summary>
        public static IReportWriter Create(ReportFormat format) => format switch
        {
            ReportFormat.Console => new ConsoleReportWriter(),
            ReportFormat.Json => new JsonReportWriter(),
            ReportFormat.Markdown => new MarkdownReportWriter(),
            ReportFormat.Sarif => new SarifReportWriter(),
            ReportFormat.Html => new HtmlReportWriter(),
            _ => new ConsoleReportWriter()
        };
    }
}
