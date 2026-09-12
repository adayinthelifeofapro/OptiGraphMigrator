using System;
using System.IO;
using System.Linq;
using OptiGraphMigrator.Core.Rules;
using Xunit;

namespace OptiGraphMigrator.Core.Tests
{
    public class RuleCatalogueLoaderTests
    {
        [Fact]
        public void Default_LoadsEmbeddedCatalogue_WithAtLeastOneRule()
        {
            var catalogue = RuleCatalogueLoader.Default;

            Assert.NotEmpty(catalogue.Rules);
        }

        [Fact]
        public void Default_EveryRule_HasUniqueId()
        {
            var catalogue = RuleCatalogueLoader.Default;

            var duplicateIds = catalogue.Rules
                .GroupBy(r => r.Id, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.Empty(duplicateIds);
        }

        [Fact]
        public void GetById_KnownRule_ReturnsRule()
        {
            var catalogue = RuleCatalogueLoader.Default;

            var rule = catalogue.GetById("OGM001");

            Assert.NotNull(rule);
            Assert.Equal("OGM001", rule!.Id);
            Assert.Equal("Filter", rule.FindSymbolPattern.MethodName);
        }

        [Fact]
        public void GetById_UnknownRule_ReturnsNull()
        {
            var catalogue = RuleCatalogueLoader.Default;

            var rule = catalogue.GetById("OGM999");

            Assert.Null(rule);
        }

        [Fact]
        public void GetCandidates_MatchesByMethodName()
        {
            var catalogue = RuleCatalogueLoader.Default;

            var candidates = catalogue.GetCandidates("Filter").ToList();

            Assert.Contains(candidates, r => r.Id == "OGM001");
        }

        [Fact]
        public void Load_WithMissingOverrideFile_ReturnsDefaultCatalogue()
        {
            var catalogue = RuleCatalogueLoader.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));

            Assert.Same(RuleCatalogueLoader.Default, catalogue);
        }

        [Fact]
        public void Load_WithOverrideFile_ReplacesMatchingRuleAndAppendsNewOnes()
        {
            var overridePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            try
            {
                File.WriteAllText(overridePath, """
                    {
                      "schemaVersion": 1,
                      "rules": [
                        {
                          "id": "OGM001",
                          "title": "Custom Filter override",
                          "severity": "warning",
                          "category": "filtering",
                          "translatability": "exact",
                          "findSymbolPattern": {
                            "containingType": "EPiServer.Find.Api.Querying.FilterExtensions",
                            "methodName": "Filter",
                            "minArguments": 1,
                            "matchDerivedTypes": true
                          },
                          "graphEquivalent": ".Where(x => <custom predicate>)",
                          "messageFormat": "custom message for {0}",
                          "isAutoFixable": false
                        },
                        {
                          "id": "OGM950",
                          "title": "Custom extra rule",
                          "severity": "info",
                          "category": "query",
                          "translatability": "caveat",
                          "findSymbolPattern": {
                            "containingType": "Some.Custom.Type",
                            "methodName": "CustomMethod",
                            "matchDerivedTypes": true
                          },
                          "graphEquivalent": ".CustomMethod()",
                          "messageFormat": "custom rule message for {0}",
                          "isAutoFixable": false
                        }
                      ]
                    }
                    """);

                var catalogue = RuleCatalogueLoader.Load(overridePath);

                var overriddenRule = catalogue.GetById("OGM001");
                Assert.NotNull(overriddenRule);
                Assert.Equal("Custom Filter override", overriddenRule!.Title);

                var newRule = catalogue.GetById("OGM950");
                Assert.NotNull(newRule);
                Assert.Equal("Custom extra rule", newRule!.Title);

                // All other default rules should still be present, unaltered.
                Assert.Equal(RuleCatalogueLoader.Default.Rules.Count + 1, catalogue.Rules.Count);
            }
            finally
            {
                File.Delete(overridePath);
            }
        }

        [Fact]
        public void ProbeForUserCatalogue_FindsFileInAncestorDirectory()
        {
            var root = Path.Combine(Path.GetTempPath(), "ogm-probe-" + Guid.NewGuid());
            var nested = Path.Combine(root, "a", "b", "c");
            Directory.CreateDirectory(nested);
            try
            {
                var catalogueFile = Path.Combine(root, RuleCatalogueLoader.UserCatalogueFileName);
                File.WriteAllText(catalogueFile, "{\"schemaVersion\":1,\"rules\":[]}");

                var found = RuleCatalogueLoader.ProbeForUserCatalogue(nested);

                Assert.Equal(catalogueFile, found);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void ProbeForUserCatalogue_WithNoFileAnywhere_ReturnsNull()
        {
            var root = Path.Combine(Path.GetTempPath(), "ogm-probe-none-" + Guid.NewGuid());
            Directory.CreateDirectory(root);
            try
            {
                var found = RuleCatalogueLoader.ProbeForUserCatalogue(root);

                Assert.Null(found);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
