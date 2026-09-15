using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace OptiGraphMigrator.Core.Analysis
{
    /// <summary>
    /// Resolves and caches the Search &amp; Navigation symbols for a single
    /// <see cref="Compilation"/>, and provides the fast gate that lets the analysers skip
    /// compilations that do not reference Find at all.
    /// </summary>
    public sealed class FindSymbolIndex
    {
        /// <summary>Known metadata names for the Find client entry point.</summary>
        public const string ClientTypeName = "EPiServer.Find.IClient";

        private static readonly string[] SearchTypeNames =
        {
            "EPiServer.Find.ITypeSearch`1",
            "EPiServer.Find.ISearch",
            "EPiServer.Find.ISearch`1",
        };

        private static readonly string[] QueryRootMethodNames =
        {
            "Search",
            "UnifiedSearch",
            "UnifiedSearchFor",
        };

        private static readonly string[] TerminalMethodNames =
        {
            "GetResult",
            "GetResultAsync",
            "GetContentResult",
            "GetContentResultAsync",
            "GetPagesResult",
            "GetFilesResult",
            "GetUnifiedSearchResult",
        };

        private readonly ImmutableArray<INamedTypeSymbol> _searchTypes;
        private readonly ImmutableArray<INamedTypeSymbol> _findRootTypes;

        private FindSymbolIndex(
            Compilation compilation,
            INamedTypeSymbol? clientType,
            ImmutableArray<INamedTypeSymbol> searchTypes,
            INamedTypeSymbol? filterBuilderType,
            INamedTypeSymbol? unifiedSearchRegistryType,
            INamedTypeSymbol? clientConventionsType,
            INamedTypeSymbol? contentLoaderType,
            ImmutableArray<INamedTypeSymbol> findRootTypes)
        {
            Compilation = compilation;
            ClientType = clientType;
            _searchTypes = searchTypes;
            FilterBuilderType = filterBuilderType;
            UnifiedSearchRegistryType = unifiedSearchRegistryType;
            ClientConventionsType = clientConventionsType;
            ContentLoaderType = contentLoaderType;
            _findRootTypes = findRootTypes;
        }

        /// <summary>The compilation this index was built for.</summary>
        public Compilation Compilation { get; }

        /// <summary><c>EPiServer.Find.IClient</c>, when referenced.</summary>
        public INamedTypeSymbol? ClientType { get; }

        /// <summary><c>EPiServer.Find.FilterBuilder`1</c>, when referenced.</summary>
        public INamedTypeSymbol? FilterBuilderType { get; }

        /// <summary>The UnifiedSearch registry type, when referenced.</summary>
        public INamedTypeSymbol? UnifiedSearchRegistryType { get; }

        /// <summary><c>EPiServer.Find.IClientConventions</c>, when referenced.</summary>
        public INamedTypeSymbol? ClientConventionsType { get; }

        /// <summary><c>EPiServer.IContentLoader</c>, when referenced.</summary>
        public INamedTypeSymbol? ContentLoaderType { get; }

        /// <summary>
        /// Fast gate. False when the compilation does not reference Find, letting the analyser
        /// return without registering any per-node callbacks.
        /// </summary>
        public bool IsFindAssemblyReferenced => !_findRootTypes.IsDefaultOrEmpty;

        /// <summary>Builds an index for the supplied compilation.</summary>
        public static FindSymbolIndex Create(Compilation compilation)
        {
            if (compilation is null)
            {
                throw new ArgumentNullException(nameof(compilation));
            }

            var clientType = compilation.GetTypeByMetadataName(ClientTypeName);

            var searchTypes = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
            foreach (var name in SearchTypeNames)
            {
                var symbol = compilation.GetTypeByMetadataName(name);
                if (symbol is not null)
                {
                    searchTypes.Add(symbol);
                }
            }

            var roots = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
            if (clientType is not null)
            {
                roots.Add(clientType);
            }

            roots.AddRange(searchTypes);

            return new FindSymbolIndex(
                compilation,
                clientType,
                searchTypes.ToImmutable(),
                compilation.GetTypeByMetadataName("EPiServer.Find.FilterBuilder`1"),
                compilation.GetTypeByMetadataName("EPiServer.Find.UnifiedSearch.UnifiedSearchRegistry"),
                compilation.GetTypeByMetadataName("EPiServer.Find.IClientConventions"),
                compilation.GetTypeByMetadataName("EPiServer.IContentLoader"),
                roots.ToImmutable());
        }

        /// <summary>
        /// True when <paramref name="type"/> is, derives from, or implements one of the Find
        /// search abstractions.
        /// </summary>
        public bool IsFindSearchType(ITypeSymbol? type)
        {
            if (type is null || _searchTypes.IsDefaultOrEmpty)
            {
                return false;
            }

            foreach (var searchType in _searchTypes)
            {
                if (IsOrInheritsFrom(type, searchType))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when the method starts a Find query.</summary>
        public bool IsQueryRoot(IMethodSymbol method)
        {
            if (method is null || !IsDeclaredInFindNamespace(method.ContainingType))
            {
                return false;
            }

            for (var i = 0; i < QueryRootMethodNames.Length; i++)
            {
                if (string.Equals(method.Name, QueryRootMethodNames[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when the method materialises a Find query.</summary>
        public bool IsTerminalCall(IMethodSymbol method)
        {
            if (method is null || !IsDeclaredInFindNamespace(method.ContainingType))
            {
                return false;
            }

            for (var i = 0; i < TerminalMethodNames.Length; i++)
            {
                if (string.Equals(method.Name, TerminalMethodNames[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when the symbol is declared somewhere under the <c>EPiServer.Find</c> namespace.
        /// Used to keep name-based method matching from colliding with unrelated APIs such as
        /// LINQ's own <c>Select</c> or a user's <c>Search</c> helper.
        /// </summary>
        public bool IsDeclaredInFindNamespace(ISymbol? symbol)
        {
            var ns = symbol?.ContainingNamespace;
            while (ns is not null && !ns.IsGlobalNamespace)
            {
                if (string.Equals(ns.ToDisplayString(), "EPiServer.Find", StringComparison.Ordinal))
                {
                    return true;
                }

                ns = ns.ContainingNamespace;
            }

            return false;
        }

        /// <summary>
        /// Resolves whether a method matches a catalogue pattern's containing type, honouring
        /// derived and implementing types.
        /// </summary>
        public bool MatchesContainingType(IMethodSymbol method, string containingTypeMetadataName, bool matchDerived)
        {
            if (string.IsNullOrEmpty(containingTypeMetadataName))
            {
                return true;
            }

            var expected = Compilation.GetTypeByMetadataName(containingTypeMetadataName);
            if (expected is null)
            {
                return false;
            }

            var actual = (ITypeSymbol?)method.ContainingType;
            if (actual is null)
            {
                return false;
            }

            return matchDerived
                ? IsOrInheritsFrom(actual, expected)
                : SymbolEqualityComparer.Default.Equals(actual.OriginalDefinition, expected.OriginalDefinition);
        }

        /// <summary>
        /// Public entry point for hand-written rules that need to test whether a resolved type
        /// is, derives from, or implements a known Find abstraction (for example
        /// <see cref="FilterBuilderType"/>).
        /// </summary>
        public bool IsAssignableTo(ITypeSymbol? type, INamedTypeSymbol? candidate)
        {
            if (type is null || candidate is null)
            {
                return false;
            }

            return IsOrInheritsFrom(type, candidate);
        }

        private static bool IsOrInheritsFrom(ITypeSymbol type, INamedTypeSymbol candidate)
        {
            var original = candidate.OriginalDefinition;

            var current = type;
            while (current is not null)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, original))
                {
                    return true;
                }

                current = current.BaseType;
            }

            foreach (var iface in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, original))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
