using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using OptiGraphMigrator.Core.Rules;

namespace OptiGraphMigrator.Analyzers
{
    /// <summary>
    /// Builds and caches <see cref="DiagnosticDescriptor"/> instances for every rule id, whether
    /// it comes from the declarative catalogue or a hand-written complex rule.
    /// </summary>
    internal static class DiagnosticDescriptors
    {
        private const string HelpLinkBase = "https://github.com/optigraphmigrator/OptiGraphMigrator/blob/main/docs/rules/";

        private static readonly ConcurrentDictionary<string, DiagnosticDescriptor> Cache =
            new ConcurrentDictionary<string, DiagnosticDescriptor>();

        /// <summary>
        /// The infrastructure diagnostic reported when a Find chain's terminal call could not be
        /// resolved (broken references, unsupported overloads, etc.).
        /// </summary>
        public static readonly DiagnosticDescriptor UnresolvedChain = new DiagnosticDescriptor(
            "OGM901",
            "Find query chain could not be fully resolved",
            "Part of this Find query chain could not be resolved to a known symbol; it may use an overload " +
            "or API surface this tool does not yet recognise, and was not analysed",
            "OptiGraphMigrator.Infrastructure",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinkBase + "OGM901.md");

        /// <summary>
        /// Reported instead of a fully-resolved rule match when Find symbols could not be
        /// resolved from metadata (e.g. a legacy CMS 11 project scanned via the source-only
        /// fallback loader) but the invocation's name still looks like Find usage. Findings
        /// carry a <c>Confidence = Heuristic</c> property and should be treated as lower
        /// confidence than semantically-resolved matches.
        /// </summary>
        public static readonly DiagnosticDescriptor HeuristicFindUsage = new DiagnosticDescriptor(
            "OGM902",
            "Possible Find usage detected heuristically",
            "This call ('{0}') looks like Optimizely Search & Navigation (Find) usage, but EPiServer.Find " +
            "references could not be resolved for this project, so it was matched by name only. Verify manually " +
            "and see the Optimizely Graph migration guidance for '{0}'",
            "OptiGraphMigrator.Heuristic",
            DiagnosticSeverity.Info,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinkBase + "OGM902.md");

        /// <summary>Gets or builds the descriptor for a declarative catalogue rule.</summary>
        public static DiagnosticDescriptor GetOrCreate(MigrationRule rule)
        {
            return Cache.GetOrAdd(rule.Id, _ => Create(
                rule.Id,
                rule.Title,
                rule.MessageFormat,
                rule.Category,
                rule.Severity,
                rule.GetDocsUrl()));
        }

        /// <summary>Gets or builds the descriptor for a hand-written complex rule.</summary>
        public static DiagnosticDescriptor GetOrCreate(IMigrationRule rule)
        {
            return Cache.GetOrAdd(rule.Id, _ => Create(
                rule.Id,
                rule.Title,
                rule.MessageFormat,
                rule.Category,
                rule.Severity,
                HelpLinkBase + rule.Id + ".md"));
        }

        private static DiagnosticDescriptor Create(
            string id,
            string title,
            string messageFormat,
            RuleCategory category,
            RuleSeverity severity,
            string helpLinkUri)
        {
            return new DiagnosticDescriptor(
                id,
                title,
                messageFormat,
                "OptiGraphMigrator." + category,
                ToDiagnosticSeverity(severity),
                isEnabledByDefault: true,
                helpLinkUri: helpLinkUri);
        }

        private static DiagnosticSeverity ToDiagnosticSeverity(RuleSeverity severity)
        {
            return severity switch
            {
                RuleSeverity.Info => DiagnosticSeverity.Info,
                RuleSeverity.Warning => DiagnosticSeverity.Warning,
                RuleSeverity.Error => DiagnosticSeverity.Warning, // never fail the build outright from a heuristic analyser
                _ => DiagnosticSeverity.Warning,
            };
        }
    }
}
