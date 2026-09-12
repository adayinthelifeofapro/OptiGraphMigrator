namespace OptiGraphMigrator.Core.Rules
{
    /// <summary>
    /// Describes how cleanly a Search &amp; Navigation (Find) construct maps onto Optimizely Graph.
    /// </summary>
    public enum Translatability
    {
        /// <summary>A mechanical, behaviour-preserving 1:1 mapping exists. Safe to auto-fix.</summary>
        Exact = 0,

        /// <summary>A mapping exists but semantics differ; a human must confirm the caveats.</summary>
        Caveat = 1,

        /// <summary>No clean translation exists; the construct must be redesigned.</summary>
        Blocked = 2,

        /// <summary>The construct was recognised as Find usage but could not be classified.</summary>
        Unknown = 3,
    }
}
