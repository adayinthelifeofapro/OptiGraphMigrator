using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EPiServer.Find.Api.Querying;

namespace EPiServer.Find
{
    /// <summary>Minimal stand-in for the real Optimizely Find SDK, just enough surface area to
    /// exercise every OptiGraphMigrator rule against representative call sites. Type and
    /// namespace names mirror the real EPiServer.Find assemblies (verified against 13.6 and 16.6)
    /// so the analyzers resolve the same symbols here as they do against a real solution.</summary>
    public interface IClient
    {
        ITypeSearch<T> Search<T>();

        ITypeSearch<T> Search<T>(System.Globalization.CultureInfo culture);

        IClientConventions Conventions { get; }

        void Index<T>(T content);

        void UpdateIndex<T>(T content);

        void DeleteIndex<T>(T content);
    }

    public interface IClientConventions
    {
    }

    /// <summary>Static singleton accessor, mirroring the real SDK convenience API <c>SearchClient.Instance</c>.</summary>
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

    public interface ISearch
    {
    }

    public interface ISearch<T> : ISearch
    {
    }

    public interface ITypeSearch<T> : ISearch<T>
    {
    }

    public interface IUnifiedSearch : ISearch
    {
        IUnifiedSearch For<T>();

        IUnifiedSearch WeightMultiplier<T>(double weight);
    }

    public sealed class FilterBuilder<T>
    {
        public FilterBuilder<T> And(Expression<Func<T, Filter>> filter) => this;

        public FilterBuilder<T> Or(Expression<Func<T, Filter>> filter) => this;
    }

    public static class Filters
    {
        public static Filter Match(this object field, object value) => null!;

        public static Filter Prefix(this object field, string value) => null!;

        public static Filter GreaterThan(this object field, object value) => null!;

        public static Filter LessThan(this object field, object value) => null!;

        public static Filter Exists(this object field) => null!;

        public static Filter In(this object field, IEnumerable<object> values) => null!;

        public static Filter InRange(this object field, object from, object to) => null!;
    }

    public static class ClientExtensions
    {
        public static FilterBuilder<T> BuildFilter<T>(this IClient client) => null!;

        public static IUnifiedSearch UnifiedSearch(this IClient client) => null!;
    }

    public static class TypeSearchExtensions
    {
        public static ITypeSearch<T> Filter<T>(this ITypeSearch<T> search, Expression<Func<T, Filter>> filter) => search;

        public static ITypeSearch<T> Filter<T>(this ITypeSearch<T> search, FilterBuilder<T> filter) => search;

        /// <summary>Graph SDK equivalent used only so code-fix output for OGM001 compiles in tests.</summary>
        public static ITypeSearch<T> Where<T>(this ITypeSearch<T> search, Expression<Func<T, Filter>> filter) => search;

        public static ITypeSearch<T> For<T>(this ITypeSearch<T> search, string text) => search;

        public static ITypeSearch<T> OrderBy<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => search;

        public static ITypeSearch<T> OrderByDescending<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => search;

        public static ITypeSearch<T> ThenBy<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => search;

        public static ITypeSearch<TResult> Select<T, TResult>(this ITypeSearch<T> search, Func<T, TResult> projection) => (ITypeSearch<TResult>)search;

        public static ITypeSearch<T> TermsFacetFor<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field, int size = 10) => search;
    }

    public static class SearchExtensions
    {
        public static ITypeSearch<T> Skip<T>(this ITypeSearch<T> search, int count) => search;

        public static ITypeSearch<T> Take<T>(this ITypeSearch<T> search, int count) => search;

        public static IEnumerable<T> GetResult<T>(this ITypeSearch<T> search) => Array.Empty<T>();

        public static Task<IEnumerable<T>> GetResultAsync<T>(this ITypeSearch<T> search) => Task.FromResult<IEnumerable<T>>(Array.Empty<T>());

        public static ITypeSearch<T> StaticallyCacheFor<T>(this ITypeSearch<T> search, TimeSpan duration) => search;
    }

    public static class QueryStringSearchExtensions
    {
        public static ITypeSearch<T> InField<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => search;

        public static ITypeSearch<T> UsingSynonyms<T>(this ITypeSearch<T> search, string synonymSet) => search;
    }

    /// <summary>Stub-only scoring helpers; the complex OGM202 rule matches these by name within the Find namespace.</summary>
    public static class CustomScoringExtensions
    {
        public static ITypeSearch<T> Boost<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field, double weight) => search;

        public static ITypeSearch<T> CustomScore<T>(this ITypeSearch<T> search, string script) => search;

        public static ITypeSearch<T> FunctionScore<T>(this ITypeSearch<T> search, string function) => search;
    }

    public static class ContentLoaderFindExtensions
    {
        public static IEnumerable<T> Search<T>(this IContentLoader loader, string query) => Array.Empty<T>();

        public static IEnumerable<T> FindByContentType<T>(this IContentLoader loader) => Array.Empty<T>();

        public static IEnumerable<T> FindPagesWithCriteria<T>(this IContentLoader loader, object criteria) => Array.Empty<T>();

        public static IEnumerable<T> GetChildrenWithFind<T>(this IContentLoader loader, ContentReference contentLink) => Array.Empty<T>();
    }

    public sealed class ContentReference
    {
        public static readonly ContentReference StartPage = new ContentReference();
    }
}

namespace EPiServer.Find.ClientConventions
{
    public static class ConventionsExtensions
    {
        public static TypeConventionBuilder<T> ForInstancesOf<T>(this IClientConventions conventions) => null!;
    }

    public sealed class TypeConventionBuilder<T>
    {
        public TypeConventionBuilder<T> ShouldIndex(Func<T, bool> predicate) => this;

        public TypeConventionBuilder<T> IncludeField(Expression<Func<T, object>> field) => this;

        public TypeConventionBuilder<T> ExcludeField(Expression<Func<T, object>> field) => this;
    }
}

namespace EPiServer.Find.Framework.Statistics
{
    public static class TrackableSearchExtensions
    {
        public static ITypeSearch<T> Track<T>(this ITypeSearch<T> search) => search;
    }

    public static class StatisticsClientExtensions
    {
        public static ITypeSearch<T> DidYouMean<T>(this ITypeSearch<T> search, string text) => search;

        public static ITypeSearch<T> Autocomplete<T>(this ITypeSearch<T> search, string text) => search;
    }
}

namespace EPiServer.Find.Cms
{
    public static class ContentSearchExtensions
    {
        public static ITypeSearch<T> FilterForVisitor<T>(this ITypeSearch<T> search) => search;

        public static ITypeSearch<T> FilterOnCurrentSite<T>(this ITypeSearch<T> search) => search;

        public static ITypeSearch<T> FilterOnLanguages<T>(this ITypeSearch<T> search, params string[] languages) => search;

        public static ITypeSearch<T> PublishedInLanguage<T>(this ITypeSearch<T> search, string language = null) => search;

        public static ITypeSearch<T> ExcludeDeleted<T>(this ITypeSearch<T> search) => search;

        public static IEnumerable<object> GetPagesResult<T>(this ITypeSearch<T> search) => Array.Empty<object>();
    }

    public static class SearchRequestExtensions
    {
        public static IEnumerable<object> GetContentResult<T>(this ITypeSearch<T> search) => Array.Empty<object>();

        public static Task<IEnumerable<object>> GetContentResultAsync<T>(this ITypeSearch<T> search) => Task.FromResult<IEnumerable<object>>(Array.Empty<object>());
    }

    /// <summary>Stub-only; the real SDK has no ForVisitorGroup(). Kept so the OGM028 sample still compiles.</summary>
    public static class SearchExtensions
    {
        public static ITypeSearch<T> ForVisitorGroup<T>(this ITypeSearch<T> search, string visitorGroupId) => search;
    }

    public static class ContentTreeExtensions
    {
        public static Filter MatchContained<T>(this ITypeSearch<T> search, ContentReference contentLink) => null!;
    }
}

namespace EPiServer.Find.Api.Querying
{
    public abstract class Filter
    {
    }

    // The extension classes below back rules OGM013-OGM018, OGM021, OGM023, OGM025, OGM026 and
    // OGM029-OGM033. The real SDK exposes these features under different names (or not at all),
    // so these stubs and their rules do not yet match real code; they are kept so the sample
    // solution continues to exercise the rule plumbing until each rule is retargeted.
    public static class HighlightExtensions
    {
        public static ITypeSearch<T> Highlight<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => search;
    }

    public static class FacetExtensions
    {
        public static ITypeSearch<T> GetFacets<T>(this ITypeSearch<T> search, Expression<Func<T, object>> field) => search;
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

namespace EPiServer
{
    public interface IContentLoader
    {
    }
}