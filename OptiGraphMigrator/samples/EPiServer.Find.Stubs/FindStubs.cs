using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace EPiServer.Find
{
    /// <summary>Minimal stand-in for the real Optimizely Find SDK, just enough surface area to
    /// exercise every OptiGraphMigrator rule against representative call sites.</summary>
    public interface IClient
    {
        Api.Querying.ITypeSearch<T> Search<T>();

        Api.Querying.ITypeSearch<T> Search<T>(System.Globalization.CultureInfo culture);

        Api.Querying.IUnifiedSearch UnifiedSearch();

        UnifiedSearch.UnifiedSearchRegistry UnifiedSearchFor { get; }

        ClientConventions Conventions { get; }

        Statistics.IStatisticTracker Statistics { get; }

        void Index<T>(T content);

        void UpdateIndex<T>(T content);

        void DeleteIndex<T>(T content);
    }

    public sealed class ClientConventions
    {
        public ClientConventions ForInstancesOf<T>() => this;

        public ClientConventions ShouldIndex(Func<object, bool> predicate) => this;

        public ClientConventions IncludeField(Expression<Func<object, object>> field) => this;

        public ClientConventions ExcludeField(Expression<Func<object, object>> field) => this;

        public ClientConventions RootType<T>() => this;
    }

    /// <summary>Static singleton accessor, mirroring the real SDK's <c>SearchClient.Instance</c> convenience API.</summary>
    public static class SearchClient
    {
        public static IClient Instance { get; } = null!;
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class SearchableAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class IncludeInDefaultSearchAttribute : Attribute
    {
    }

    public static class ClientExtensions
    {
        public static IClient Track<T>(this IClient client, string query, IEnumerable<T> results) => client;
    }

    namespace Statistics
    {
        public interface IStatisticTracker
        {
            IReadOnlyList<string> GetPopularSearchTerms(int count);

            IReadOnlyList<int> GetSearchesPerDay(DateTime from, DateTime to);
        }
    }

    namespace UnifiedSearch
    {
        public sealed class UnifiedSearchRegistry
        {
            public UnifiedSearchRegistry For<T>() => this;

            public UnifiedSearchRegistry WeightMultiplier<T>(double weight) => this;
        }
    }

    namespace Api.Querying
    {
        public interface ISearch
        {
        }

        public interface ISearch<T> : ISearch
        {
        }

        public interface IUnifiedSearch : ISearch
        {
            IUnifiedSearch For<T>();
        }

        public interface ITypeSearch<T> : ISearch<T>
        {
        }

        public interface IFilterBuilder
        {
            IFilterBuilder And(IFilterBuilder other);

            IFilterBuilder Or(IFilterBuilder other);

            IFilterBuilder Not();

            static IFilterBuilder MatchAll() => null!;

            static IFilterBuilder MatchNone() => null!;
        }

        public static class FilterExtensions
        {
            public static ITypeSearch<T> Filter<T>(this ITypeSearch<T> search, Func<T, IFilterBuilder> filter) => search;

            /// <summary>Graph SDK equivalent used only so code-fix output for OGM001 compiles in tests.</summary>
            public static ITypeSearch<T> Where<T>(this ITypeSearch<T> search, Func<T, IFilterBuilder> filter) => search;
        }

        public static class FieldFilterExtensions
        {
            public static IFilterBuilder Match(this object field, object value) => null!;

            public static IFilterBuilder StartsWith(this object field, string value) => null!;

            public static IFilterBuilder GreaterThan(this object field, object value) => null!;

            public static IFilterBuilder LessThan(this object field, object value) => null!;

            public static IFilterBuilder Exists(this object field) => null!;

            public static IFilterBuilder In(this object field, IEnumerable<object> values) => null!;

            public static IFilterBuilder Between(this object field, object from, object to) => null!;
        }

        public static class OrderByExtensions
        {
            public static ITypeSearch<T> OrderBy<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => search;

            public static ITypeSearch<T> OrderByDescending<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => search;

            public static ITypeSearch<T> ThenBy<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => search;
        }

        public static class SelectExtensions
        {
            public static ITypeSearch<TResult> Select<T, TResult>(this ITypeSearch<T> search, Func<T, TResult> projection) => (ITypeSearch<TResult>)search;
        }

        public static class ITypeSearchExtensions
        {
            public static ITypeSearch<T> For<T>(this ITypeSearch<T> search, string text) => search;

            public static ITypeSearch<T> Skip<T>(this ITypeSearch<T> search, int count) => search;

            public static ITypeSearch<T> Take<T>(this ITypeSearch<T> search, int count) => search;

            public static IEnumerable<T> GetResult<T>(this ITypeSearch<T> search) => Array.Empty<T>();

            public static Task<IEnumerable<T>> GetResultAsync<T>(this ITypeSearch<T> search) => Task.FromResult<IEnumerable<T>>(Array.Empty<T>());
        }

        public static class CacheExtensions
        {
            public static ITypeSearch<T> StaticallyCacheFor<T>(this ITypeSearch<T> search, TimeSpan duration) => search;
        }

        public static class CustomScoringExtensions
        {
            public static ITypeSearch<T> Boost<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field, double weight) => search;

            public static ITypeSearch<T> CustomScore<T>(this ITypeSearch<T> search, string script) => search;

            public static ITypeSearch<T> FunctionScore<T>(this ITypeSearch<T> search, string function) => search;

            public static ITypeSearch<T> InField<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => search;

            public static ITypeSearch<T> UsingSynonyms<T>(this ITypeSearch<T> search, string synonymSet) => search;
        }

        public static class HighlightExtensions
        {
            public static ITypeSearch<T> Highlight<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => search;
        }

        public static class FacetExtensions
        {
            public static ITypeSearch<T> GetFacets<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => search;

            public static ITypeSearch<T> TermsFacetFor<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field, int size = 10) => search;

            public static ITypeSearch<T> RangeFacetFor<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field, params object[] ranges) => search;
        }

        public static class RangeExtensions
        {
            public static ITypeSearch<T> Range<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field, object from, object to) => search;
        }

        public static class LanguageExtensions
        {
            public static ITypeSearch<T> Language<T>(this ITypeSearch<T> search, string languageCode) => search;
        }

        public static class TotalMatchingExtensions
        {
            public static int TotalMatching<T>(this ITypeSearch<T> search) => 0;
        }

        public static class StatisticsExtensions
        {
            public static double AverageOf<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => 0;

            public static double SumOf<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => 0;

            public static object MaximumOf<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => null!;

            public static object MinimumOf<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => null!;
        }

        public static class FuzzyExtensions
        {
            public static ITypeSearch<T> Fuzzy<T>(this ITypeSearch<T> search, double similarity = 0.5) => search;
        }

        public static class PromotedExtensions
        {
            public static ITypeSearch<T> BestBets<T>(this ITypeSearch<T> search, string query) => search;
        }

        public static class SuggestExtensions
        {
            public static ITypeSearch<T> DidYouMean<T>(this ITypeSearch<T> search, string text) => search;

            public static ITypeSearch<T> Autocomplete<T>(this ITypeSearch<T> search, string text) => search;
        }

        public static class GeoExtensions
        {
            public static ITypeSearch<T> DistanceFrom<T>(this ITypeSearch<T> search, double latitude, double longitude) => search;
        }

        public static class SimilarityExtensions
        {
            public static ITypeSearch<T> MoreLikeThis<T>(this ITypeSearch<T> search, T content) => search;
        }

        public static class DeduplicationExtensions
        {
            public static ITypeSearch<T> RemoveDuplicates<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => search;
        }

        public static class ScoreExtensions
        {
            public static ITypeSearch<T> MinScore<T>(this ITypeSearch<T> search, double threshold) => search;
        }
    }

    namespace Cms
    {
        public interface IContentSearch<T>
        {
        }

        public static class SearchExtensions
        {
            public static Api.Querying.ITypeSearch<T> FilterForVisitor<T>(this Api.Querying.ITypeSearch<T> search) => search;

            public static Api.Querying.ITypeSearch<T> ForVisitorGroup<T>(this Api.Querying.ITypeSearch<T> search, string visitorGroupId) => search;
        }

        public static class SearchContentExtensions
        {
            public static IEnumerable<object> GetContentResult<T>(this Api.Querying.ITypeSearch<T> search) => Array.Empty<object>();

            public static Task<IEnumerable<object>> GetContentResultAsync<T>(this Api.Querying.ITypeSearch<T> search) => Task.FromResult<IEnumerable<object>>(Array.Empty<object>());
        }

        public static class ContentTreeExtensions
        {
            public static Api.Querying.IFilterBuilder MatchContained<T>(this Api.Querying.ITypeSearch<T> search, ContentReference contentLink) => null!;
        }
    }
}

namespace EPiServer
{
    public interface IContentLoader
    {
    }
}

namespace EPiServer.Find
{
    public static class ContentLoaderFindExtensions
    {
        public static System.Collections.Generic.IEnumerable<T> Search<T>(this IContentLoader loader, string query) => System.Array.Empty<T>();

        public static System.Collections.Generic.IEnumerable<T> FindByContentType<T>(this IContentLoader loader) => System.Array.Empty<T>();

        public static System.Collections.Generic.IEnumerable<T> FindPagesWithCriteria<T>(this IContentLoader loader, object criteria) => System.Array.Empty<T>();

        public static System.Collections.Generic.IEnumerable<T> GetChildrenWithFind<T>(this IContentLoader loader, ContentReference contentLink) => System.Array.Empty<T>();
    }

    public sealed class ContentReference
    {
        public static readonly ContentReference StartPage = new ContentReference();
    }
}
