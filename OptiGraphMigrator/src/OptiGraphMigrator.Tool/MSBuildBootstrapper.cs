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
                var instance = MSBuildLocator.QueryVisualStudioInstances()
                    .Where(i => i.DiscoveryType == DiscoveryType.VisualStudioSetup || i.DiscoveryType == DiscoveryType.DeveloperConsole)
                    .OrderByDescending(i => i.Version)
                    .FirstOrDefault();

                if (instance is not null)
                {
                    MSBuildLocator.RegisterInstance(instance);
                    return new MSBuildBootstrapResult(true, $"{instance.Name} {instance.Version}");
                }

                errorWriter.WriteLine(
                    "warning: this looks like a legacy (non-SDK) Optimizely CMS 11 project, but no Visual Studio/Build Tools MSBuild instance was found. Falling back to the .NET SDK MSBuild, which may fail to load the project; a source-only heuristic scan will be attempted if it does.");
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
