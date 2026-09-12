using System;

namespace OptiGraphMigrator.Core.Rules.Complex
{
    /// <summary>
    /// Flags indexing convention configuration (<c>ClientConventions</c>,
    /// <c>ShouldIndex</c>, <c>IncludeField</c>) that controls what Find puts in its index.
    /// </summary>
    /// <remarks>
    /// Optimizely Graph has an equivalent concept (content type / field configuration in the
    /// Graph admin schema) but it is configured outside application code, so this can never be
    /// auto-fixed.
    /// </remarks>
    public sealed class ClientConventionsRule : IMigrationRule
    {
        /// <inheritdoc />
        public string Id => "OGM103";

        /// <inheritdoc />
        public string Title => "Find indexing conventions must move to Graph schema configuration";

        /// <inheritdoc />
        public RuleSeverity Severity => RuleSeverity.Warning;

        /// <inheritdoc />
        public RuleCategory Category => RuleCategory.Configuration;

        /// <inheritdoc />
        public string MessageFormat => "Find indexing convention '{0}(...)' has no in-code Graph equivalent; configure the schema in Graph instead";

        /// <inheritdoc />
        public bool AppliesTo(RuleMatchContext context)
        {
            var method = context.Segment.Method;

            if (context.Symbols.ClientConventionsType is not null &&
                context.Symbols.IsAssignableTo(context.Segment.ReceiverType, context.Symbols.ClientConventionsType))
            {
                return true;
            }

            return context.Symbols.IsDeclaredInFindNamespace(method.ContainingType) &&
                   (string.Equals(method.Name, "ShouldIndex", StringComparison.Ordinal) ||
                    string.Equals(method.Name, "IncludeField", StringComparison.Ordinal) ||
                    string.Equals(method.Name, "ExcludeField", StringComparison.Ordinal) ||
                    string.Equals(method.Name, "RootType", StringComparison.Ordinal) ||
                    string.Equals(method.Name, "ForInstancesOf", StringComparison.Ordinal));
        }

        /// <inheritdoc />
        public RuleMatch? Evaluate(RuleMatchContext context)
        {
            if (!AppliesTo(context))
            {
                return null;
            }

            var caveats = new[]
            {
                "Find indexing conventions are declared in application code and take effect on the next index " +
                "job. Optimizely Graph content type and field configuration is instead managed through the Graph " +
                "admin UI / content type synchronisation, so this configuration must be moved out of code and " +
                "re-created there.",
            };

            return new RuleMatch(
                Id,
                context.Segment.Invocation.GetLocation(),
                Translatability.Caveat,
                "Configure the equivalent content type / field in the Optimizely Graph schema",
                caveats,
                graphQlSnippet: null,
                messageArguments: new object?[] { context.Segment.MethodName });
        }
    }
}
