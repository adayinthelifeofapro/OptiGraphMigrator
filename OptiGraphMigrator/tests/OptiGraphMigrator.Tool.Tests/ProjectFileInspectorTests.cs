using System.IO;
using OptiGraphMigrator.Tool;
using Xunit;

namespace OptiGraphMigrator.Tool.Tests
{
    public class ProjectFileInspectorTests
    {
        [Fact]
        public void Inspect_LegacyCms11Project_DetectsLegacyAndPackagesConfig()
        {
            var result = ProjectFileInspector.Inspect(SamplePaths.SampleCms11SolutionCsproj);

            Assert.True(result.HasLegacyProject);
            Assert.True(result.HasPackagesConfig);
            Assert.Equal("v4.7.2", result.TargetFrameworkVersion);
            Assert.Equal("13.6.0", result.FindPackageVersion);
            Assert.Equal("11.20.0", result.CmsCorePackageVersion);
            Assert.Equal("11", result.DetectedCmsVersion);
        }

        [Fact]
        public void Inspect_SdkStyleProject_DoesNotDetectLegacyProject()
        {
            var result = ProjectFileInspector.Inspect(SamplePaths.SampleSolutionCsproj);

            Assert.False(result.HasLegacyProject);
            Assert.False(result.HasPackagesConfig);
        }

        [Fact]
        public void Inspect_MissingFile_ReturnsEmptyResult()
        {
            var result = ProjectFileInspector.Inspect(Path.Combine(Path.GetTempPath(), "does-not-exist.csproj"));

            Assert.False(result.HasLegacyProject);
            Assert.False(result.HasPackagesConfig);
            Assert.Null(result.DetectedCmsVersion);
        }
    }
}
