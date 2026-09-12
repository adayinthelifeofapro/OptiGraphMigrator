using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OptiGraphMigrator.Core.Analysis;
using OptiGraphMigrator.Core.Rules;

namespace OptiGraphMigrator.Core.Emit
{
    /// <summary>
    /// A rule match resolved against a specific segment of a <see cref="FindChain"/>.
    /// </summary>
    public sealed class SegmentTranslation
    {
        /// <summary>Creates a segment translation.</summary>
        public SegmentTranslation(FindChainSegment segment, RuleMatch? match)
        {
            Segment = segment;
            Match = match;
        }

        /// <summary>The chain segment this translation covers.</summary>
        public FindChainSegment Segment { get; }

        /// <summary>The rule match produced for this segment, or <c>null</c> when no rule fired.</summary>
        public RuleMatch? Match { get; }
    }

    /// <summary>
    /// Composes an illustrative Optimizely Graph GraphQL query from a matched
    /// <see cref="FindChain"/> and the per-segment rule matches the engine produced for it.
    /// </summary>
    /// <remarks>
    /// This is a best-effort preview, not a guaranteed-correct query: segments that could not be
    /// translated are represented as <c>\# TODO</c> comments so the reader sees exactly what
    /// still needs manual attention.
    /// </remarks>
    public static class GraphQlSnippetEmitter
    {
        /// <summary>Composes the GraphQL snippet for a translated chain.</summary>
        public static string Emit(FindChain chain, IReadOnlyList<SegmentTranslation> translations)
        {
            if (chain is null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            if (translations is null)
            {
                throw new ArgumentNullException(nameof(translations));
            }

            var whereClauses = new List<string>();
            var orderByClauses = new List<string>();
            var selectionFields = new List<string>();
            string? skip = null;
            string? limit = null;
            var todos = new List<string>();

            foreach (var translation in translations)
            {
                var match = translation.Match;
                if (match is null)
                {
                    continue;
                }

                var isUsable = match.Translatability is Translatability.Exact or Translatability.Caveat &&
                               !string.IsNullOrWhiteSpace(match.GraphQlSnippet);

                if (!isUsable)
                {
                    todos.Add(BuildTodoComment(translation));
                    continue;
                }

                var snippet = match.GraphQlSnippet!.Trim();
                Classify(snippet, whereClauses, orderByClauses, selectionFields, ref skip, ref limit, todos, translation);
            }

            return Compose(chain, whereClauses, orderByClauses, selectionFields, skip, limit, todos);
        }

        private static void Classify(
            string snippet,
            List<string> whereClauses,
            List<string> orderByClauses,
            List<string> selectionFields,
            ref string? skip,
            ref string? limit,
            List<string> todos,
            SegmentTranslation translation)
        {
            if (snippet.StartsWith("where:", StringComparison.Ordinal))
            {
                whereClauses.Add(snippet.Substring("where:".Length).Trim());
            }
            else if (snippet.StartsWith("orderBy:", StringComparison.Ordinal))
            {
                orderByClauses.Add(snippet.Substring("orderBy:".Length).Trim());
            }
            else if (snippet.StartsWith("skip:", StringComparison.Ordinal))
            {
                skip = snippet.Substring("skip:".Length).Trim();
            }
            else if (snippet.StartsWith("limit:", StringComparison.Ordinal))
            {
                limit = snippet.Substring("limit:".Length).Trim();
            }
            else if (snippet.StartsWith("items", StringComparison.Ordinal))
            {
                selectionFields.Add(snippet);
            }
            else
            {
                // A snippet we don't have a structural slot for (e.g. a bare query fragment
                // contributed by a terminal call rule) - surface it verbatim as a comment so
                // nothing is silently dropped.
                todos.Add("# " + translation.Segment.MethodName + "(...) contributes: " + snippet);
            }
        }

        private static string BuildTodoComment(SegmentTranslation translation)
        {
            var reason = translation.Match?.Caveats.Count > 0
                ? translation.Match.Caveats[0]
                : "no automatic Graph translation is available for this construct";

            return "# TODO: " + translation.Segment.MethodName + "(...) - " + reason;
        }

        private static string Compose(
            FindChain chain,
            List<string> whereClauses,
            List<string> orderByClauses,
            List<string> selectionFields,
            string? skip,
            string? limit,
            List<string> todos)
        {
            var contentTypeName = chain.SearchedType?.Name ?? "Content";
            var builder = new StringBuilder();

            builder.Append("query {\n  ").Append(contentTypeName).Append('(');

            var args = new List<string>();
            if (whereClauses.Count == 1)
            {
                args.Add("where: { " + whereClauses[0] + " }");
            }
            else if (whereClauses.Count > 1)
            {
                args.Add("where: { AND: [ " + string.Join(", ", whereClauses.Select(w => "{ " + w + " }")) + " ] }");
            }

            if (orderByClauses.Count > 0)
            {
                args.Add("orderBy: { " + string.Join(", ", orderByClauses) + " }");
            }

            if (skip is not null)
            {
                args.Add("skip: " + skip);
            }

            if (limit is not null)
            {
                args.Add("limit: " + limit);
            }

            builder.Append(string.Join(", ", args));
            builder.Append(") {\n    items {\n");

            if (selectionFields.Count > 0)
            {
                foreach (var field in selectionFields)
                {
                    builder.Append("      ").Append(field).Append('\n');
                }
            }
            else
            {
                builder.Append("      # TODO: list the fields this query needs\n");
            }

            builder.Append("    }\n  }\n}");

            if (todos.Count > 0)
            {
                builder.Append('\n');
                foreach (var todo in todos)
                {
                    builder.Append(todo).Append('\n');
                }
            }

            return builder.ToString();
        }
    }
}
