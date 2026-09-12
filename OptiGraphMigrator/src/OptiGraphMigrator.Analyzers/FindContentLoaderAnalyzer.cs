using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using OptiGraphMigrator.Core.Analysis;
using OptiGraphMigrator.Core.Rules;

namespace OptiGraphMigrator.Analyzers
{
    /// <summary>
    /// Reports Find-backed <c>IContentLoader</c> helper extension methods (for example
    /// <c>contentLoader.Search&lt;T&gt;()</c>) so they can be migrated to an explicit Graph SDK
    /// query.
    /// </summary>
    /// <remarks>
    /// Evaluated per invocation rather than through the fluent chain walker, since these helpers
    /// are invoked on <c>IContentLoader</c>, not one of the recognised Find query-root types.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class FindContentLoaderAnalyzer : DiagnosticAnalyzer
    {
        private static readonly ImmutableArray<DiagnosticDescriptor> Descriptors = ImmutableArray.Create(
            DiagnosticDescriptors.GetOrCreate(ComplexRuleRegistry.ContentLoaderHelper));

        /// <inheritdoc />
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Descriptors;

        /// <inheritdoc />
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                var symbols = FindSymbolIndex.Create(compilationStartContext.Compilation);
                if (!symbols.IsFindAssemblyReferenced || symbols.ContentLoaderType is null)
                {
                    return;
                }

                compilationStartContext.RegisterSyntaxNodeAction(
                    nodeContext => Analyze(nodeContext, symbols),
                    SyntaxKind.InvocationExpression);
            });
        }

        private static void Analyze(SyntaxNodeAnalysisContext context, FindSymbolIndex symbols)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            var ruleContext = StandaloneRuleContextFactory.TryCreate(invocation, context.SemanticModel, symbols, context.CancellationToken);
            if (ruleContext is null)
            {
                return;
            }

            var rule = ComplexRuleRegistry.ContentLoaderHelper;
            if (!rule.AppliesTo(ruleContext))
            {
                return;
            }

            var match = rule.Evaluate(ruleContext);
            if (match is null)
            {
                return;
            }

            var properties = ImmutableDictionary.CreateBuilder<string, string?>();
            properties.Add("RuleId", match.RuleId);
            properties.Add("GraphEquivalent", match.GraphEquivalent);
            properties.Add("GraphQlSnippet", match.GraphQlSnippet ?? string.Empty);
            properties.Add("Translatability", match.Translatability.ToString());

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.GetOrCreate(rule),
                match.Location,
                properties.ToImmutable(),
                match.MessageArguments.ToArray()));
        }
    }
}
