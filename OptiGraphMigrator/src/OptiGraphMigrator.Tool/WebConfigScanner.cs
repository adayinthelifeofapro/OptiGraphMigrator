using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using OptiGraphMigrator.Reporting;

namespace OptiGraphMigrator.Tool
{
    /// <summary>
    /// Scans <c>web.config</c> (and standalone <c>episerver.find.config</c>) files for Find's
    /// declarative configuration - the <c>episerver.find</c> section and
    /// <c>EPiServer.Find.Framework</c> initialization - which have no counterpart in
    /// <c>Microsoft.CodeAnalysis</c>'s syntax/symbol model and so are invisible to the Roslyn
    /// analysers. Emits findings pointing at the equivalent Optimizely Graph configuration.
    /// </summary>
    internal static class WebConfigScanner
    {
        private const string RuleId = "OGM040";

        public static IReadOnlyList<MigrationFinding> Scan(string solutionRoot)
        {
            var findings = new List<MigrationFinding>();
            if (string.IsNullOrEmpty(solutionRoot) || !Directory.Exists(solutionRoot))
            {
                return findings;
            }

            foreach (var configPath in EnumerateConfigFiles(solutionRoot))
            {
                XDocument document;
                try
                {
                    document = XDocument.Load(configPath);
                }
                catch (Exception)
                {
                    continue;
                }

                var findSection = document.Descendants("episerver.find").FirstOrDefault();
                if (findSection is null)
                {
                    continue;
                }

                var serviceUrl = findSection.Attribute("serviceUrl")?.Value;
                var defaultIndex = findSection.Attribute("defaultIndex")?.Value;
                var relativePath = MakeRelative(solutionRoot, configPath);

                var message = string.IsNullOrEmpty(serviceUrl)
                    ? "Found an <episerver.find> configuration section; Find's serviceUrl/defaultIndex settings have no Optimizely Graph equivalent"
                    : $"Found an <episerver.find> configuration section (serviceUrl='{serviceUrl}', defaultIndex='{defaultIndex}'); these have no Optimizely Graph equivalent";

                findings.Add(new MigrationFinding(
                    RuleId,
                    "warning",
                    message,
                    relativePath,
                    1,
                    1,
                    1,
                    1,
                    graphEquivalent: "Optimizely Graph app settings (gateway address, single/app key) and DI registration via AddOptimizelyGraph(...)",
                    graphQlSnippet: null,
                    title: "episerver.find configuration must move to Optimizely Graph configuration",
                    translatability: "caveat",
                    docsUrl: null,
                    suggestedApproach:
                        "Remove the <episerver.find> section and register the Optimizely Graph client instead, " +
                        "supplying its gateway address and credentials via appsettings.json/DI, not web.config."));
            }

            return findings;
        }

        private static IEnumerable<string> EnumerateConfigFiles(string root)
        {
            foreach (var fileName in new[] { "web.config", "episerver.find.config" })
            {
                foreach (var file in SafeEnumerateFiles(root, fileName))
                {
                    yield return file;
                }
            }
        }

        private static IEnumerable<string> SafeEnumerateFiles(string root, string fileName)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(root, fileName, SearchOption.AllDirectories);
            }
            catch (IOException)
            {
                yield break;
            }
            catch (UnauthorizedAccessException)
            {
                yield break;
            }

            foreach (var file in files)
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return file;
            }
        }

        private static string MakeRelative(string root, string filePath)
        {
            try
            {
                var rootUri = new Uri(root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar);
                var fileUri = new Uri(filePath);
                return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
            }
            catch (UriFormatException)
            {
                return filePath;
            }
        }
    }
}
