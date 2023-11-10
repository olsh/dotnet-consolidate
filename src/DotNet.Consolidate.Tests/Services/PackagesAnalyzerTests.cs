using System.Collections.Generic;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;

using Xunit;

namespace DotNet.Consolidate.Tests.Services;

public class PackagesAnalyzerTests
{
    [Fact]
    public void Versions_with_trailing_zeroes_are_the_same()
    {
        // This case may happen when you have mixed project types in your solution
        var analyzer = new PackagesAnalyzer();
        var info = new ProjectInfo("Test", "Test", new List<NuGetPackageInfo>
        {
            new ("myid", new Version("1.0.1"), NuGetPackageReferenceType.Direct),
            new ("myid", new Version("1.0.1.0"), NuGetPackageReferenceType.Direct)
        });
        var projectInfos = new List<ProjectInfo> { info };
        var options = new Options(new List<string>(), new List<string>(), new List<string>(), string.Empty, true, true);
        var result = analyzer.FindNonConsolidatedPackages(projectInfos, options);

        Assert.All(result, analysisResult => Assert.False(analysisResult.ContainsDifferentPackagesVersions));
    }

    [Fact]
    public void Packages_with_different_versions_are_not_consolidated()
    {
        var analyzer = new PackagesAnalyzer();
        var info = new ProjectInfo("Test", "Test", new List<NuGetPackageInfo>()
        {
            new ("myid", new Version("1.1.0"), NuGetPackageReferenceType.Direct),
            new ("myid", new Version("1.0.1.0"), NuGetPackageReferenceType.Direct)
        });
        var projectInfos = new List<ProjectInfo> { info };
        var options = new Options(new List<string>(), new List<string>(), new List<string>(), string.Empty, true, true);
        var result = analyzer.FindNonConsolidatedPackages(projectInfos, options);

        Assert.All(result, analysisResult => Assert.True(analysisResult.ContainsDifferentPackagesVersions));
    }

    [Theory]
    [InlineData(".*-alpha$", true)]
    [InlineData(".*-beta$", false)]
    public void Packages_version_exclude_regex_correctly_matches(string excludedPackageVersionsRegex, bool shouldMatch)
    {
        var analyzer = new PackagesAnalyzer();
        var info = new ProjectInfo("Test", "Test", new List<NuGetPackageInfo>()
        {
            new ("myid", new Version("1.1.0-alpha"), NuGetPackageReferenceType.Direct),
            new ("myid", new Version("1.0.1.0"), NuGetPackageReferenceType.Direct)
        });
        var projectInfos = new List<ProjectInfo> { info };
        var options = new Options(new List<string>(), new List<string>(), new List<string>(), excludedPackageVersionsRegex, true, true);
        var result = analyzer.FindNonConsolidatedPackages(projectInfos, options);

        Assert.All(result, analysisResult => Assert.NotEqual(shouldMatch, analysisResult.ContainsDifferentPackagesVersions));
    }
}
