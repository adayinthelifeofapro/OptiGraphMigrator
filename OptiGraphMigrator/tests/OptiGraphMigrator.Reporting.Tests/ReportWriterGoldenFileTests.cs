using System;
using System.IO;
using Xunit;

namespace OptiGraphMigrator.Reporting.Tests
{
    public class ReportWriterGoldenFileTests
    {
        private static string GoldenFilesDirectory =>
            Path.Combine(AppContext.BaseDirectory, "GoldenFiles");

        [Fact]
        public void ConsoleWriter_MatchesGoldenFile()
        {
            AssertMatchesGoldenFile(new ConsoleReportWriter(), "console.txt");
        }

        [Fact]
        public void JsonWriter_MatchesGoldenFile()
        {
            AssertMatchesGoldenFile(new JsonReportWriter(), "json.txt");
        }

        [Fact]
        public void SarifWriter_MatchesGoldenFile()
        {
            AssertMatchesGoldenFile(new SarifReportWriter(), "sarif.txt");
        }

        [Fact]
        public void MarkdownWriter_MatchesGoldenFile()
        {
            AssertMatchesGoldenFile(new MarkdownReportWriter(), "markdown.txt");
        }

        private static void AssertMatchesGoldenFile(IReportWriter writer, string goldenFileName)
        {
            var report = SampleReport.Create();
            using var stringWriter = new StringWriter();
            writer.Write(report, stringWriter);
            var actual = Normalize(stringWriter.ToString());

            var goldenFilePath = Path.Combine(GoldenFilesDirectory, goldenFileName);
            var expected = Normalize(File.ReadAllText(goldenFilePath));

            Assert.Equal(expected, actual);
        }

        private static string Normalize(string text) => text.Replace("\r\n", "\n").Trim();
    }
}
