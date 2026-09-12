using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OptiGraphMigrator.Analyzers;
using OptiGraphMigrator.Core.Rules;

namespace OptiGraphMigrator.CodeFixes
{
    /// <summary>
    /// Offers automatic fixes for Search &amp; Navigation calls whose Optimizely Graph
    /// mapping is mechanical (<see cref="Translatability.Exact"/>) and explicitly flagged as
    /// <see cref="MigrationRule.IsAutoFixable"/>. Currently this only covers renaming the
    /// invoked method to its Graph SDK equivalent (for example <c>Filter(...)</c> to
    /// <c>Where(...)</c>); the arguments themselves are left untouched for manual review.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(FindChainCodeFixProvider))]
    [Shared]
    public sealed class FindChainCodeFixProvider : CodeFixProvider
    {
        private static readonly ImmutableArray<string> FixableIds = BuildFixableIds();

        /// <inheritdoc />
        public override ImmutableArray<string> FixableDiagnosticIds => FixableIds;

        /// <inheritdoc />
        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        /// <inheritdoc />
        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var document = context.Document;
            var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return;
            }

            foreach (var diagnostic in context.Diagnostics)
            {
                if (!diagnostic.Properties.TryGetValue("RuleId", out var ruleId) || ruleId is null)
                {
                    continue;
                }

                var rule = RuleCatalogueLoader.Default.GetById(ruleId);
                if (rule is null || !rule.IsAutoFixable)
                {
                    continue;
                }

                var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
                var invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>(ancestor => ancestor.Span.Contains(diagnostic.Location.SourceSpan));
                if (invocation is not { Expression: MemberAccessExpressionSyntax memberAccess })
                {
                    continue;
                }

                var graphMethodName = GraphMethodName(rule);
                if (graphMethodName is null || memberAccess.Name.Identifier.Text == graphMethodName)
                {
                    continue;
                }

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Rename to Graph '{graphMethodName}(...)'",
                        createChangedDocument: cancellationToken =>
                            RenameMethodAsync(document, root, memberAccess, graphMethodName, cancellationToken),
                        equivalenceKey: rule.Id),
                    diagnostic);
            }
        }

        private static Task<Document> RenameMethodAsync(
            Document document,
            SyntaxNode root,
            MemberAccessExpressionSyntax memberAccess,
            string newMethodName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var newName = memberAccess.Name.WithIdentifier(
                Microsoft.CodeAnalysis.CSharp.SyntaxFactory.Identifier(newMethodName)
                    .WithTriviaFrom(memberAccess.Name.Identifier));

            var newMemberAccess = memberAccess.WithName(newName);
            var newRoot = root.ReplaceNode(memberAccess, newMemberAccess);

            return Task.FromResult(document.WithSyntaxRoot(newRoot));
        }

        /// <summary>
        /// Extracts the Graph SDK method name from <see cref="MigrationRule.GraphEquivalent"/>,
        /// which is expressed as source text such as <c>.Where(x =&gt; &lt;translated predicate&gt;)</c>.
        /// </summary>
        private static string? GraphMethodName(MigrationRule rule)
        {
            var text = rule.GraphEquivalent.TrimStart('.');
            var parenIndex = text.IndexOf('(');
            if (parenIndex <= 0)
            {
                return null;
            }

            return text.Substring(0, parenIndex);
        }

        private static ImmutableArray<string> BuildFixableIds()
        {
            var builder = ImmutableArray.CreateBuilder<string>();

            foreach (var rule in RuleCatalogueLoader.Default.Rules)
            {
                if (rule.IsAutoFixable)
                {
                    builder.Add(rule.Id);
                }
            }

            return builder.ToImmutable();
        }
    }
}
