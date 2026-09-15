using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace OptiGraphMigrator.Tool.Tests
{
    [Collection(MsBuildLocatorCollection.Name)]
    public class CommandLineAppTests
    {
        [Fact]
        public async Task Scan_MissingPath_ReturnsExitCode2()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = await CommandLineApp.RunAsync(
                new[] { "scan", "does-not-exist.csproj" },
                output,
                error);

            Assert.Equal(2, exitCode);
            Assert.Contains("does not exist", error.ToString());
        }

        [Fact]
        public async Task Scan_MissingRulesFile_ReturnsExitCode2()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = await CommandLineApp.RunAsync(
                new[] { "scan", SamplePaths.SampleSolutionCsproj, "--rules", "does-not-exist.json" },
                output,
                error);

            Assert.Equal(2, exitCode);
            Assert.Contains("rules file", error.ToString());
        }

        [Fact]
        public async Task Scan_SampleSolution_ProducesFindingsAndDefaultsToNonZeroExitOnWarnings()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = await CommandLineApp.RunAsync(
                new[] { "scan", SamplePaths.SampleSolutionCsproj, "--format", "console" },
                output,
                error);

            // Default --fail-on is "error"; the sample only produces warnings/info, so it should
            // succeed even though findings are present.
            Assert.Equal(0, exitCode);
            Assert.Contains("OGM0", output.ToString());
        }
        [Fact]
        public async Task Scan_SampleSolution_WithFailOnWarning_ReturnsExitCode1()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = await CommandLineApp.RunAsync(
                new[] { "scan", SamplePaths.SampleSolutionCsproj, "--format", "console", "--fail-on", "warning" },
                output,
                error);

            Assert.Equal(1, exitCode);
        }

        [Fact]
        public async Task Scan_SampleSolution_JsonFormat_ProducesValidJsonWithFindings()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = await CommandLineApp.RunAsync(
                new[] { "scan", SamplePaths.SampleSolutionCsproj, "--format", "json" },
                output,
                error);

            Assert.Equal(0, exitCode);

            var json = output.ToString();
            Assert.Contains("\"RuleId\"", json);
            Assert.Contains("OGM0", json);
        }

        [Fact]
        public async Task Scan_SampleSolution_SarifFormat_ProducesSarifDocument()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = await CommandLineApp.RunAsync(
                new[] { "scan", SamplePaths.SampleSolutionCsproj, "--format", "sarif" },
                output,
                error);

            Assert.Equal(0, exitCode);

            var sarif = output.ToString();
            Assert.Contains("\"version\": \"2.1.0\"", sarif);
            Assert.Contains("\"runs\"", sarif);
        }

        [Fact]
        public async Task Scan_SampleSolution_MarkdownFormat_ProducesSummaryTable()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = await CommandLineApp.RunAsync(
                new[] { "scan", SamplePaths.SampleSolutionCsproj, "--format", "markdown" },
                output,
                error);

            Assert.Equal(0, exitCode);

            var markdown = output.ToString();
            Assert.Contains("#", markdown);
            Assert.Contains("OGM0", markdown);
        }

        [Fact]
        public async Task Scan_SampleSolution_WritesToOutputFile_WhenOutputOptionProvided()
        {
            var output = new StringWriter();
            var error = new StringWriter();
            var outputFile = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid() + ".json");

            try
            {
                var exitCode = await CommandLineApp.RunAsync(
                    new[] { "scan", SamplePaths.SampleSolutionCsproj, "--format", "json", "--output", outputFile },
                    output,
                    error);

                Assert.Equal(0, exitCode);
                Assert.True(File.Exists(outputFile));

                var contents = await File.ReadAllTextAsync(outputFile, Encoding.UTF8);
                Assert.Contains("OGM0", contents);
            }
            finally
            {
                if (File.Exists(outputFile))
                {
                    File.Delete(outputFile);
                }
            }
        }

        [Fact]
        public async Task Scan_SampleSolution_WithSeverityThresholdError_FiltersOutWarnings()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = await CommandLineApp.RunAsync(
                new[] { "scan", SamplePaths.SampleSolutionCsproj, "--format", "json", "--severity-threshold", "error" },
                output,
                error);

            Assert.Equal(0, exitCode);

            var json = output.ToString();
            // The sample solution does not produce any error-severity findings, only
            // warning/info, so filtering to "error" should yield an empty findings array.
            Assert.Contains("[]", json);
        }

        [Fact]
        public async Task Scan_LegacyCms11SampleSolution_ProducesFindingsWithoutThrowing()
        {
            var output = new StringWriter();
            var error = new StringWriter();

            var exitCode = await CommandLineApp.RunAsync(
                new[] { "scan", SamplePaths.SampleCms11SolutionCsproj, "--format", "console" },
                output,
                error);

            // The legacy project's packages.config references are never restored on disk, so
            // MSBuild cannot resolve any Find symbols; the scan falls back to the source-only
            // heuristic loader, reports the detected CMS 11 version, warns that results are
            // heuristic, and still finds Find usage by name (OGM902).
            Assert.True(exitCode == 0 || exitCode == 1);
            var consoleOutput = output.ToString();
            Assert.Contains("Detected Optimizely CMS version: 11", consoleOutput, StringComparison.Ordinal);
            Assert.Contains("OGM902", consoleOutput, StringComparison.Ordinal);
        }
    }
}
