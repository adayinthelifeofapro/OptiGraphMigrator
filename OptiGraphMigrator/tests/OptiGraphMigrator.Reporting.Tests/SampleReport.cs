using System.Collections.Generic;

namespace OptiGraphMigrator.Reporting.Tests
{
    /// <summary>Shared fixed input used by the report writer golden-file tests.</summary>
    internal static class SampleReport
    {
        public static MigrationReport Create()
        {
            var findings = new List<MigrationFinding>
            {
                new MigrationFinding(
                    ruleId: "OGM001",
                    severity: "warning",
                    message: "Find 'Filter(...)' can be translated to a Graph 'where' clause",
                    filePath: "FindUsageSamples.cs",
                    startLine: 10,
                    startColumn: 5,
                    endLine: 10,
                    endColumn: 25,
                    graphEquivalent: "where",
                    graphQlSnippet: "{ where: { title: { eq: \"foo\" } } }",
                    title: "Filter maps to where clause",
                    translatability: "exact",
                    docsUrl: "https://github.com/optigraphmigrator/OptiGraphMigrator/blob/main/docs/rules/OGM001.md"),
                new MigrationFinding(
                    ruleId: "OGM202",
                    severity: "info",
                    message: "Find custom scoring 'Boost(...)' has no clean Graph SDK translation",
                    filePath: "FindUsageSamples.cs",
                    startLine: 20,
                    startColumn: 8,
                    endLine: 20,
                    endColumn: 30,
                    graphEquivalent: null,
                    graphQlSnippet: null,
                    title: "Custom scoring has no Graph equivalent",
                    translatability: "blocked",
                    docsUrl: "https://github.com/optigraphmigrator/OptiGraphMigrator/blob/main/docs/rules/OGM202.md",
                    suggestedApproach: "Optimizely Graph has no per-query boost function. Retrieve the candidate set from Graph, then apply the boost weights in application code when ordering the results.")
            };

            return new MigrationReport(findings);
        }
    }
}
