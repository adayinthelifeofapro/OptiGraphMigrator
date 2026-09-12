namespace OptiGraphMigrator.Core.Rules
{
    /// <summary>
    /// Logical grouping for a migration rule. Drives report sectioning and diagnostic categories.
    /// </summary>
    public enum RuleCategory
    {
        /// <summary>IClient / ITypeSearch fluent query composition.</summary>
        Query = 0,

        /// <summary>Filtering, including custom IFilterBuilder composition.</summary>
        Filtering = 1,

        /// <summary>Projection and result materialisation.</summary>
        Projection = 2,

        /// <summary>UnifiedSearch multi-type querying.</summary>
        UnifiedSearch = 3,

        /// <summary>Indexing conventions and client configuration.</summary>
        Configuration = 4,

        /// <summary>Statistics, tracking and analytics APIs.</summary>
        Statistics = 5,

        /// <summary>Find-backed IContentLoader helpers.</summary>
        ContentLoading = 6,

        /// <summary>Tool infrastructure (load failures, unresolved symbols).</summary>
        Infrastructure = 7,
    }
}
