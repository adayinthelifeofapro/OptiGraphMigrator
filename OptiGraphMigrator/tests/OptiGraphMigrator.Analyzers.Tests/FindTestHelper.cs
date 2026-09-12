using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

namespace OptiGraphMigrator.Analyzers.Tests
{
    /// <summary>
    /// Shared test scaffolding that wires the EPiServer.Find stub assembly into every
    /// analyzer/code-fix test so rule detection can be exercised against realistic call shapes.
    /// </summary>
    internal static class FindTestHelper
    {
        public static ReferenceAssemblies ReferenceAssemblies { get; } = ReferenceAssemblies.Net.Net80;

        public static async Task VerifyAnalyzerAsync<TAnalyzer>(string source, params DiagnosticResult[] expected)
            where TAnalyzer : DiagnosticAnalyzer, new()
        {
            var test = new CSharpAnalyzerTest<TAnalyzer, XUnitVerifier>
            {
                TestCode = source,
                ReferenceAssemblies = ReferenceAssemblies,
            };

            AddFindReference(test.TestState.AdditionalReferences);
            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }

        public static async Task VerifyCodeFixAsync<TAnalyzer, TCodeFix>(
            string source,
            string fixedSource,
            params DiagnosticResult[] expected)
            where TAnalyzer : DiagnosticAnalyzer, new()
            where TCodeFix : CodeFixProvider, new()
        {
            var test = new CSharpCodeFixTest<TAnalyzer, TCodeFix, XUnitVerifier>
            {
                TestCode = source,
                FixedCode = fixedSource,
                ReferenceAssemblies = ReferenceAssemblies,
            };

            AddFindReference(test.TestState.AdditionalReferences);
            AddFindReference(test.FixedState.AdditionalReferences);
            test.ExpectedDiagnostics.AddRange(expected);

            await test.RunAsync();
        }

        private static void AddFindReference(ICollection<MetadataReference> references)
        {
            references.Add(MetadataReference.CreateFromFile(typeof(EPiServer.Find.IClient).Assembly.Location));
        }
    }
}
