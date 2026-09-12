using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;
using OptiGraphMigrator.Reporting;

namespace OptiGraphMigrator.Tool
{
    /// <summary>Builds and runs the <c>optigraph-migrate</c> command line.</summary>
    internal static class CommandLineApp
    {
        public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter errorWriter)
        {
            var pathArgument = new Argument<string>("path", "Path to a solution (.sln/.slnx) or project file to scan.");

            var outputOption = new Option<string?>("--output", "File path to write the report to. Defaults to stdout.");
            outputOption.AddAlias("-o");

            var formatOption = new Option<string>(
                "--format",
                () => "console",
                "Report format: console, sarif, json, markdown, or html.");

            var rulesOption = new Option<string?>(
                "--rules",
                "Path to a rule catalogue file overriding/extending the built-in Find-to-Graph mappings.");

            var severityThresholdOption = new Option<string>(
                "--severity-threshold",
                () => "info",
                "Minimum severity (info, warning, error) a finding must have to be included in the report.");

            var failOnOption = new Option<string>(
                "--fail-on",
                () => "error",
                "Minimum severity (info, warning, error) that causes a non-zero exit code.");

            var scanCommand = new Command("scan", "Scans a solution or project for Find usage and reports the Optimizely Graph migration path.")
            {
                pathArgument,
                outputOption,
                formatOption,
                rulesOption,
                severityThresholdOption,
                failOnOption
            };

            scanCommand.SetHandler(async (InvocationContext context) =>
            {
                var path = context.ParseResult.GetValueForArgument(pathArgument);
                var options = new ScanOptions
                {
                    Path = path,
                    Output = context.ParseResult.GetValueForOption(outputOption),
                    Format = ReportFormatParser.Parse(context.ParseResult.GetValueForOption(formatOption)),
                    RulesPath = context.ParseResult.GetValueForOption(rulesOption),
                    SeverityThreshold = context.ParseResult.GetValueForOption(severityThresholdOption) ?? "info",
                    FailOn = context.ParseResult.GetValueForOption(failOnOption) ?? "error"
                };

                context.ExitCode = await RunScanAsync(options, output, errorWriter).ConfigureAwait(false);
            });

            var root = new RootCommand("Scans a solution for Optimizely Search & Navigation (Find) usage and reports the Optimizely Graph migration path.")
            {
                scanCommand
            };

            return await root.InvokeAsync(args).ConfigureAwait(false);
        }

        private static async Task<int> RunScanAsync(ScanOptions options, TextWriter output, TextWriter errorWriter)
        {
            if (!File.Exists(options.Path))
            {
                errorWriter.WriteLine($"error: '{options.Path}' does not exist.");
                return 2;
            }

            if (!string.IsNullOrEmpty(options.RulesPath) && !File.Exists(options.RulesPath))
            {
                errorWriter.WriteLine($"error: rules file '{options.RulesPath}' does not exist.");
                return 2;
            }

            MigrationReport report;
            try
            {
                report = await ScanEngine.ScanAsync(options, errorWriter).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                errorWriter.WriteLine($"error: scan failed - {ex.Message}");
                return 2;
            }

            var writer = ReportWriterFactory.Create(options.Format);

            if (string.IsNullOrEmpty(options.Output))
            {
                writer.Write(report, output);
            }
            else
            {
                using var fileWriter = new StreamWriter(options.Output!);
                writer.Write(report, fileWriter);
            }

            var shouldFail = false;
            if (report.ErrorCount > 0)
            {
                shouldFail = SeverityLevel.Meets("error", options.FailOn);
            }
            else if (report.WarningCount > 0)
            {
                shouldFail = SeverityLevel.Meets("warning", options.FailOn);
            }
            else if (report.InfoCount > 0)
            {
                shouldFail = SeverityLevel.Meets("info", options.FailOn);
            }

            return shouldFail ? 1 : 0;
        }
    }
}
