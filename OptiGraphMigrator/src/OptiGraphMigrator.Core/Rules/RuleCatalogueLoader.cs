using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OptiGraphMigrator.Core.Rules
{
    /// <summary>
    /// Root document shape of a rule catalogue file.
    /// </summary>
    public sealed class RuleCatalogueDocument
    {
        /// <summary>Catalogue schema version, bumped on breaking shape changes.</summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>The declarative rules.</summary>
        public IList<MigrationRule> Rules { get; set; } = new List<MigrationRule>();
    }

    /// <summary>
    /// An immutable, indexed set of declarative migration rules.
    /// </summary>
    public sealed class RuleCatalogue
    {
        private readonly IReadOnlyDictionary<string, MigrationRule> _byId;
        private readonly ILookup<string, MigrationRule> _byMethodName;

        internal RuleCatalogue(IReadOnlyList<MigrationRule> rules)
        {
            Rules = rules;
            _byId = rules.ToDictionary(r => r.Id, StringComparer.Ordinal);
            _byMethodName = rules.ToLookup(r => r.FindSymbolPattern.MethodName, StringComparer.Ordinal);
        }

        /// <summary>All rules in the catalogue.</summary>
        public IReadOnlyList<MigrationRule> Rules { get; }

        /// <summary>Looks up a rule by its diagnostic id.</summary>
        public MigrationRule? GetById(string id)
        {
            return _byId.TryGetValue(id, out var rule) ? rule : null;
        }

        /// <summary>
        /// Candidate rules for a method name. This is the hot path pre-filter; callers must
        /// still confirm the containing type against the resolved symbol.
        /// </summary>
        public IEnumerable<MigrationRule> GetCandidates(string methodName)
        {
            return _byMethodName[methodName];
        }
    }

    /// <summary>
    /// Loads the embedded rule catalogue and merges optional user overrides from disk so the
    /// Find-to-Graph mappings can be updated without shipping a new build.
    /// </summary>
    public static class RuleCatalogueLoader
    {
        /// <summary>Conventional file name looked for alongside the scanned solution.</summary>
        public const string UserCatalogueFileName = "optigraph.rules.json";

        private const string EmbeddedResourceName =
            "OptiGraphMigrator.Core.Resources.find-to-graph.rules.json";

        private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

        private static RuleCatalogue? _default;
        private static readonly object DefaultGate = new object();

        /// <summary>
        /// The built-in catalogue. Parsed once and cached, since analysers construct this per
        /// compilation.
        /// </summary>
        public static RuleCatalogue Default
        {
            get
            {
                if (_default is null)
                {
                    lock (DefaultGate)
                    {
                        _default ??= new RuleCatalogue(LoadEmbedded());
                    }
                }

                return _default;
            }
        }

        /// <summary>
        /// Loads the built-in catalogue merged with an optional override file. Rules in the
        /// override file replace built-in rules with the same id, and any additional rules are
        /// appended.
        /// </summary>
        /// <param name="userCataloguePath">Path to a user catalogue, or <c>null</c>.</param>
        public static RuleCatalogue Load(string? userCataloguePath)
        {
            if (string.IsNullOrWhiteSpace(userCataloguePath) || !File.Exists(userCataloguePath))
            {
                return Default;
            }

            RuleCatalogueDocument overrides;
            using (var stream = File.OpenRead(userCataloguePath!))
            {
                overrides = Deserialize(stream, userCataloguePath!);
            }

            var merged = new List<MigrationRule>(Default.Rules);
            foreach (var rule in overrides.Rules)
            {
                rule.Validate();

                var existing = merged.FindIndex(r => string.Equals(r.Id, rule.Id, StringComparison.Ordinal));
                if (existing >= 0)
                {
                    merged[existing] = rule;
                }
                else
                {
                    merged.Add(rule);
                }
            }

            return new RuleCatalogue(new ReadOnlyCollection<MigrationRule>(merged));
        }

        /// <summary>
        /// Probes for <see cref="UserCatalogueFileName"/> in the given directory and each of its
        /// ancestors, mirroring how .editorconfig style files are discovered.
        /// </summary>
        public static string? ProbeForUserCatalogue(string? startDirectory)
        {
            if (string.IsNullOrWhiteSpace(startDirectory))
            {
                return null;
            }

            var directory = new DirectoryInfo(startDirectory!);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, UserCatalogueFileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return null;
        }

        private static IReadOnlyList<MigrationRule> LoadEmbedded()
        {
            var assembly = typeof(RuleCatalogueLoader).GetTypeInfo().Assembly;
            using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName);
            if (stream is null)
            {
                throw new InvalidOperationException(
                    "Embedded rule catalogue '" + EmbeddedResourceName + "' was not found. " +
                    "Available resources: " + string.Join(", ", assembly.GetManifestResourceNames()));
            }

            var document = Deserialize(stream, EmbeddedResourceName);
            foreach (var rule in document.Rules)
            {
                rule.Validate();
            }

            return new ReadOnlyCollection<MigrationRule>(document.Rules.ToList());
        }

        private static RuleCatalogueDocument Deserialize(Stream stream, string source)
        {
            try
            {
                var document = JsonSerializer.Deserialize<RuleCatalogueDocument>(stream, SerializerOptions);
                if (document is null)
                {
                    throw new InvalidOperationException("Rule catalogue '" + source + "' was empty.");
                }

                if (document.SchemaVersion != 1)
                {
                    throw new InvalidOperationException(
                        "Rule catalogue '" + source + "' declares unsupported schemaVersion " +
                        document.SchemaVersion + "; this build understands version 1.");
                }

                return document;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    "Rule catalogue '" + source + "' is not valid JSON: " + ex.Message, ex);
            }
        }

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
            return options;
        }
    }
}
