using System;
using System.Collections.Generic;

namespace OptiGraphMigrator.Core.Rules
{
    /// <summary>
    /// Identifies the Search &amp; Navigation API shape a <see cref="MigrationRule"/> applies to.
    /// Matching is performed against resolved symbols, never against raw syntax text.
    /// </summary>
    public sealed class FindSymbolPattern
    {
        /// <summary>
        /// Metadata name of the declaring type, for example
        /// <c>EPiServer.Find.Api.Querying.ITypeSearch`1</c>. For extension methods this is the
        /// static class that declares the method.
        /// </summary>
        public string ContainingType { get; set; } = string.Empty;

        /// <summary>Name of the method being invoked, for example <c>Filter</c>.</summary>
        public string MethodName { get; set; } = string.Empty;

        /// <summary>
        /// Optional inclusive lower bound on the supplied argument count, used to disambiguate
        /// overloads. <c>null</c> means "any".
        /// </summary>
        public int? MinArguments { get; set; }

        /// <summary>Optional inclusive upper bound on the supplied argument count.</summary>
        public int? MaxArguments { get; set; }

        /// <summary>
        /// When true the pattern also matches methods declared on types derived from, or
        /// implementing, <see cref="ContainingType"/>.
        /// </summary>
        public bool MatchDerivedTypes { get; set; } = true;
    }

    /// <summary>
    /// A declarative Find-to-Graph mapping loaded from the rule catalogue.
    /// </summary>
    public sealed class MigrationRule
    {
        /// <summary>Stable diagnostic id, for example <c>OGM001</c>.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Short human readable title.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Reporting severity.</summary>
        public RuleSeverity Severity { get; set; } = RuleSeverity.Warning;

        /// <summary>Logical grouping used by the reports.</summary>
        public RuleCategory Category { get; set; } = RuleCategory.Query;

        /// <summary>How cleanly this construct maps onto Graph.</summary>
        public Translatability Translatability { get; set; } = Translatability.Caveat;

        /// <summary>The Find API shape this rule matches.</summary>
        public FindSymbolPattern FindSymbolPattern { get; set; } = new FindSymbolPattern();

        /// <summary>
        /// The Optimizely Graph SDK equivalent, expressed as source text, for example
        /// <c>.Where(...)</c>. Empty when <see cref="Translatability"/> is
        /// <see cref="Translatability.Blocked"/>.
        /// </summary>
        public string GraphEquivalent { get; set; } = string.Empty;

        /// <summary>
        /// GraphQL fragment contributed by this construct. Supports <c>{0}</c>-style
        /// placeholders filled in by the snippet emitter.
        /// </summary>
        public string GraphQlSnippetTemplate { get; set; } = string.Empty;

        /// <summary>Behavioural differences a human must review before accepting the mapping.</summary>
        public IList<string> Caveats { get; set; } = new List<string>();

        /// <summary>Explicit documentation link. Falls back to a conventional per-rule doc path.</summary>
        public string? DocsUrl { get; set; }

        /// <summary>Message template shown in the diagnostic. <c>{0}</c> is the matched method name.</summary>
        public string MessageFormat { get; set; } = string.Empty;

        /// <summary>
        /// True when the mapping is mechanical enough for the code fix provider to apply it
        /// automatically. Only ever honoured for <see cref="Translatability.Exact"/> rules.
        /// </summary>
        public bool IsAutoFixable { get; set; }

        /// <summary>Resolves the effective documentation link for this rule.</summary>
        public string GetDocsUrl()
        {
            if (!string.IsNullOrWhiteSpace(DocsUrl))
            {
                return DocsUrl!;
            }

            return "https://github.com/optigraphmigrator/OptiGraphMigrator/blob/main/docs/rules/" + Id + ".md";
        }

        /// <summary>Validates that the rule carries the minimum data the engine needs.</summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id))
            {
                throw new InvalidOperationException("A migration rule must declare an Id.");
            }

            if (string.IsNullOrWhiteSpace(FindSymbolPattern.MethodName))
            {
                throw new InvalidOperationException("Rule " + Id + " must declare FindSymbolPattern.MethodName.");
            }

            if (Translatability != Translatability.Blocked && string.IsNullOrWhiteSpace(GraphEquivalent))
            {
                throw new InvalidOperationException("Rule " + Id + " is translatable but declares no GraphEquivalent.");
            }

            if (IsAutoFixable && Translatability != Translatability.Exact)
            {
                throw new InvalidOperationException("Rule " + Id + " is marked auto-fixable but is not an exact mapping.");
            }
        }
    }
}
