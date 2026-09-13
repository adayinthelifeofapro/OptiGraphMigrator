using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OptiGraphMigrator.Reporting
{
    /// <summary>Writes findings as a minimal SARIF 2.1.0 log, for GitHub code scanning / CI annotation.</summary>
    public sealed class SarifReportWriter : IReportWriter
    {
        private const string ToolName = "OptiGraphMigrator";
        private const string InformationUri = "https://github.com/optigraphmigrator/OptiGraphMigrator";

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <inheritdoc />
        public void Write(MigrationReport report, TextWriter writer)
        {
            var rules = report.Findings
                .GroupBy(f => f.RuleId)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var sample = g.First();
                    var rule = new SarifRule { Id = g.Key };
                    if (!string.IsNullOrEmpty(sample.Title))
                    {
                        rule.ShortDescription = new SarifMessage { Text = sample.Title! };
                    }

                    if (!string.IsNullOrEmpty(sample.DocsUrl))
                    {
                        rule.HelpUri = sample.DocsUrl;
                    }

                    return rule;
                })
                .ToList();

            var results = report.Findings.Select(finding => new SarifResult
            {
                RuleId = finding.RuleId,
                Level = ToSarifLevel(finding.Severity),
                Message = new SarifMessage { Text = ToResultText(finding) },
                Locations = new List<SarifLocation>
                {
                    new SarifLocation
                    {
                        PhysicalLocation = new SarifPhysicalLocation
                        {
                            ArtifactLocation = new SarifArtifactLocation { Uri = finding.FilePath.Replace('\\', '/') },
                            Region = new SarifRegion
                            {
                                StartLine = finding.StartLine,
                                StartColumn = finding.StartColumn,
                                EndLine = finding.EndLine,
                                EndColumn = finding.EndColumn
                            }
                        }
                    }
                }
            }).ToList();

            var log = new SarifLog
            {
                Runs = new List<SarifRun>
                {
                    new SarifRun
                    {
                        Tool = new SarifTool
                        {
                            Driver = new SarifDriver
                            {
                                Name = ToolName,
                                InformationUri = InformationUri,
                                Rules = rules
                            }
                        },
                        Results = results
                    }
                }
            };

            var json = JsonSerializer.Serialize(log, Options);
            writer.Write(json);
            writer.WriteLine();
        }

        private static string ToResultText(MigrationFinding finding)
        {
            // SARIF results carry a single message, so the guidance is appended to it rather than
            // hidden in a property bag most viewers (including GitHub code scanning) do not surface.
            return string.IsNullOrEmpty(finding.SuggestedApproach)
                ? finding.Message
                : finding.Message + " Suggested approach: " + finding.SuggestedApproach;
        }

        private static string ToSarifLevel(string severity) => severity switch
        {
            "error" => "error",
            "warning" => "warning",
            _ => "note"
        };

        private sealed class SarifLog
        {
            public string Version { get; set; } = "2.1.0";

            [JsonPropertyName("$schema")]
            public string Schema { get; set; } = "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json";
            public List<SarifRun> Runs { get; set; } = new List<SarifRun>();
        }

        private sealed class SarifRun
        {
            public SarifTool Tool { get; set; } = new SarifTool();
            public List<SarifResult> Results { get; set; } = new List<SarifResult>();
        }

        private sealed class SarifTool
        {
            public SarifDriver Driver { get; set; } = new SarifDriver();
        }

        private sealed class SarifDriver
        {
            public string Name { get; set; } = string.Empty;
            public string InformationUri { get; set; } = string.Empty;
            public List<SarifRule> Rules { get; set; } = new List<SarifRule>();
        }

        private sealed class SarifRule
        {
            public string Id { get; set; } = string.Empty;
            public SarifMessage? ShortDescription { get; set; }
            public string? HelpUri { get; set; }
        }

        private sealed class SarifResult
        {
            public string RuleId { get; set; } = string.Empty;
            public string Level { get; set; } = "warning";
            public SarifMessage Message { get; set; } = new SarifMessage();
            public List<SarifLocation> Locations { get; set; } = new List<SarifLocation>();
        }

        private sealed class SarifMessage
        {
            public string Text { get; set; } = string.Empty;
        }

        private sealed class SarifLocation
        {
            public SarifPhysicalLocation PhysicalLocation { get; set; } = new SarifPhysicalLocation();
        }

        private sealed class SarifPhysicalLocation
        {
            public SarifArtifactLocation ArtifactLocation { get; set; } = new SarifArtifactLocation();
            public SarifRegion Region { get; set; } = new SarifRegion();
        }

        private sealed class SarifArtifactLocation
        {
            public string Uri { get; set; } = string.Empty;
        }

        private sealed class SarifRegion
        {
            public int StartLine { get; set; }
            public int StartColumn { get; set; }
            public int EndLine { get; set; }
            public int EndColumn { get; set; }
        }
    }
}
