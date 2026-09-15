using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Locator;

namespace OptiGraphMigrator.Tool
{
    /// <summary>Describes the MSBuild instance selected by <see cref="MSBuildBootstrapper"/>.</summary>
    public readonly struct MSBuildBootstrapResult
    {
        public MSBuildBootstrapResult(bool isLegacyModeActive, string? registeredInstanceName)
        {
            IsLegacyModeActive = isLegacyModeActive;
            RegisteredInstanceName = registeredInstanceName;
        }

        /// <summary>True when the scan target looked like a legacy (non-SDK) CMS 11 style project.</summary>
        public bool IsLegacyModeActive { get; }

        /// <summary>Friendly name/version of the MSBuild instance that was registered, if known.</summary>
        public string? RegisteredInstanceName { get; }
    }

    /// <summary>
    /// Chooses and registers an MSBuild instance with <see cref="MSBuildLocator"/> before any
    /// MSBuild or Roslyn workspace type is touched. For legacy (non-SDK, .NET Framework, CMS 11
    /// style) targets, a full Visual Studio / Build Tools MSBuild instance is preferred, since it
    /// can evaluate legacy web/class-library projects and packages.config references. For
    /// everything else, the default (.NET SDK) MSBuild is used.
    /// </summary>
    /// <remarks>
    /// This class must only use <see cref="ProjectFileInspector"/> (plain XML/text reading) and
    /// <see cref="MSBuildLocator"/> APIs. It must never reference
    /// <c>Microsoft.CodeAnalysis.MSBuild</c> or any other MSBuild-dependent type, since those
    /// cannot be loaded until an MSBuild instance has been registered.
    ///
    /// <para>
    /// <see cref="MSBuildLocator.QueryVisualStudioInstances()"/> can only discover full Visual
    /// Studio installations when the .NET Framework (net46) build of Microsoft.Build.Locator is
    /// loaded, because the COM-based VS Setup discovery code is compiled out of the .NET (Core)
    /// build (guarded by the <c>FEATURE_VISUALSTUDIOSETUP</c> constant, which is only defined for
    /// the net46 target). Since this tool runs on .NET, that API can only ever return .NET SDK
    /// instances - even when Visual Studio is installed.
    /// </para>
    /// <para>
    /// A full Visual Studio/Build Tools MSBuild toolset also cannot be loaded in-process here even
    /// via a manual path (e.g. discovered through <c>vswhere.exe</c> and registered with
    /// <see cref="MSBuildLocator.RegisterMSBuildPath(string)"/>): that toolset only ships
    /// .NET Framework (net46) MSBuild assemblies, which are not binary-compatible with this
    /// .NET 10 process and fail with assembly version/manifest mismatches at runtime (e.g.
    /// <c>Microsoft.Bcl.AsyncInterfaces</c>). Therefore, for legacy/CMS 11 style solutions, this
    /// bootstrapper always falls back to the .NET SDK MSBuild and relies on
    /// <c>ScanEngine</c>'s source-only heuristic scan when the SDK MSBuild cannot evaluate the
    /// legacy project files.
    /// </para>
    /// </remarks>
    public static class MSBuildBootstrapper
    {
        public static MSBuildBootstrapResult RegisterFor(string[] args, TextWriter errorWriter)
        {
            if (MSBuildLocator.IsRegistered)
            {
                return new MSBuildBootstrapResult(false, null);
            }

            var scanPath = TryFindScanPath(args);
            var inspection = scanPath is not null
                ? ProjectFileInspector.Inspect(scanPath)
                : new ProjectFileInspectionResult();

            if (inspection.HasLegacyProject || inspection.HasPackagesConfig)
            {
                errorWriter.WriteLine(
                    "warning: this looks like a legacy (non-SDK) Optimizely CMS 11 project. A full Visual Studio/Build Tools MSBuild instance cannot be used from this .NET tool, so the .NET SDK MSBuild will be used, which may fail to load the project; a source-only heuristic scan will be attempted if it does.");
            }

            MSBuildLocator.RegisterDefaults();
            return new MSBuildBootstrapResult(inspection.HasLegacyProject || inspection.HasPackagesConfig, null);
        }

        private static string? TryFindScanPath(string[] args)
        {
            var scanIndex = Array.FindIndex(args, a => string.Equals(a, "scan", StringComparison.OrdinalIgnoreCase));
            var searchStart = scanIndex >= 0 ? scanIndex + 1 : 0;

            for (var i = searchStart; i < args.Length; i++)
            {
                var candidate = args[i];
                if (candidate.StartsWith("-", StringComparison.Ordinal))
                {
                    continue;
                }

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
