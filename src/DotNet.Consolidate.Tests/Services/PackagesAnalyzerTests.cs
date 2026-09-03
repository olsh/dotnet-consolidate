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
        var info = new ProjectInfo(
            "Test",
            "Test",
            new List<NuGetPackageInfo>
            {
                new NuGetPackageInfo("myid", new Version("1.0.1"), NuGetPackageReferenceType.Direct),
                new NuGetPackageInfo("myid", new Version("1.0.1.0"), NuGetPackageReferenceType.Direct)
            });
        var projectInfos = new List<ProjectInfo> { info };
        var options = new Options();
        var result = analyzer.FindNonConsolidatedPackages(projectInfos, options);

        Assert.All(result, analysisResult => Assert.False(analysisResult.ContainsDifferentPackagesVersions));
    }

    [Fact]
    public void Packages_with_different_versions_are_not_consolidated()
    {
        var analyzer = new PackagesAnalyzer();
        var info = new ProjectInfo(
            "Test",
            "Test",
            new List<NuGetPackageInfo>()
            {
                new NuGetPackageInfo("myid", new Version("1.1.0"), NuGetPackageReferenceType.Direct),
                new NuGetPackageInfo("myid", new Version("1.0.1.0"), NuGetPackageReferenceType.Direct)
            });
        var projectInfos = new List<ProjectInfo> { info };
        var options = new Options();
        var result = analyzer.FindNonConsolidatedPackages(projectInfos, options);

        Assert.All(result, analysisResult => Assert.True(analysisResult.ContainsDifferentPackagesVersions));
    }

    [Theory]
    [InlineData(".*-alpha$", true)]
    [InlineData(".*-beta$", false)]
    [InlineData("", false)]
    public void Packages_version_exclude_regex_correctly_matches(string excludedPackageVersionsRegex, bool shouldMatch)
    {
        var analyzer = new PackagesAnalyzer();
        var info = new ProjectInfo(
            "Test",
            "Test",
            new List<NuGetPackageInfo>()
            {
                new NuGetPackageInfo("myid", new Version("1.1.0-alpha"), NuGetPackageReferenceType.Direct),
                new NuGetPackageInfo("myid", new Version("1.0.1.0"), NuGetPackageReferenceType.Direct)
            });
        var projectInfos = new List<ProjectInfo> { info };
        var options = new Options { ExcludedPackageVersionsRegex = excludedPackageVersionsRegex };
        var result = analyzer.FindNonConsolidatedPackages(projectInfos, options);

        Assert.All(
            result,
            analysisResult => Assert.NotEqual(shouldMatch, analysisResult.ContainsDifferentPackagesVersions));
    }

    [Fact]
    public void Package_ids_given_with_p_are_matched_case_insensitively()
    {
        var analyzer = new PackagesAnalyzer();
        var options = new Options { PackageIds = new List<string> { "serilog" } };

        var result = analyzer.FindNonConsolidatedPackages(CreateSerilogProjects(), options);

        var analysisResult = Assert.Single(result);
        Assert.Equal("Serilog", analysisResult.NuGetPackageId);
    }

    [Fact]
    public void Package_ids_given_with_e_are_matched_case_insensitively()
    {
        var analyzer = new PackagesAnalyzer();
        var options = new Options { ExcludedPackageIds = new List<string> { "serilog" } };

        var result = analyzer.FindNonConsolidatedPackages(CreateSerilogProjects(), options);

        Assert.Empty(result);
    }

    [Fact]
    public void Packages_differing_only_in_case_are_the_same_package()
    {
        // NuGet package IDs are case-insensitive, so this is one package at two versions, not two packages
        // each consolidated with itself.
        var analyzer = new PackagesAnalyzer();
        var projectInfos = new List<ProjectInfo>
        {
            CreateProject("ProjectA", "Serilog", "1.0.0"),
            CreateProject("ProjectB", "serilog", "2.0.0")
        };

        var result = analyzer.FindNonConsolidatedPackages(projectInfos, new Options());

        var analysisResult = Assert.Single(result);
        Assert.Equal(2, analysisResult.PackageVersions.Count);
    }

    [Fact]
    public void Package_ids_present_in_a_different_case_are_not_reported_as_missing()
    {
        var analyzer = new PackagesAnalyzer();

        var result = analyzer.FindPackageIdsNotInSolution(
            new List<ProjectInfo> { CreateProject("ProjectA", "Serilog", "1.0.0") },
            new List<string> { "serilog" });

        Assert.Empty(result);
    }

    [Fact]
    public void Package_ids_absent_from_the_solution_are_reported_as_missing()
    {
        var analyzer = new PackagesAnalyzer();

        var result = analyzer.FindPackageIdsNotInSolution(
            new List<ProjectInfo> { CreateProject("ProjectA", "Serilog", "1.0.0") },
            new List<string> { "NotReferenced" });

        // Echoed back as the user typed it.
        Assert.Equal(new[] { "NotReferenced" }, result);
    }

    /// <summary>
    /// Two projects referencing <c>Serilog</c> at different versions, so it is not consolidated.
    /// </summary>
    private static List<ProjectInfo> CreateSerilogProjects()
    {
        return new List<ProjectInfo>
        {
            CreateProject("ProjectA", "Serilog", "1.0.0"),
            CreateProject("ProjectB", "Serilog", "2.0.0")
        };
    }

    private static ProjectInfo CreateProject(string projectName, string packageId, string version)
    {
        return new ProjectInfo(
            projectName,
            projectName,
            new List<NuGetPackageInfo>
            {
                new NuGetPackageInfo(packageId, new Version(version), NuGetPackageReferenceType.Direct)
            });
    }
}
