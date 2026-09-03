using System.Collections.Generic;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;

using Xunit;

namespace DotNet.Consolidate.Tests.Services;

public class PackagesAnalyzerTests
{
    private const string PropsFile = @"C:\src\Directory.Build.props";

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

    [Fact]
    public void A_package_declared_by_both_the_project_and_its_props_file_is_an_override()
    {
        var analyzer = new PackagesAnalyzer();

        var result = analyzer.FindDirectoryBuildPropsOverrides(
            new List<ProjectInfo> { CreateOverridingProject("Serilog", "4.0.0", "Serilog", "3.0.1") },
            new Options());

        var propsOverride = Assert.Single(result);
        Assert.Equal("ProjectB", propsOverride.ProjectName);
        Assert.Equal("Serilog", propsOverride.PackageId);
        Assert.Equal("4.0.0", propsOverride.ProjectVersion.OriginalValue);
        Assert.Equal("3.0.1", propsOverride.DirectoryBuildPropsVersion.OriginalValue);
        Assert.Equal(PropsFile, propsOverride.DirectoryBuildPropsFile);
    }

    [Fact]
    public void An_override_that_repeats_the_inherited_version_is_still_reported()
    {
        // Redeclaring the package is a duplicate item to MSBuild whatever the version, and the copy silently
        // stops following the props file the next time it is bumped.
        var analyzer = new PackagesAnalyzer();

        var result = analyzer.FindDirectoryBuildPropsOverrides(
            new List<ProjectInfo> { CreateOverridingProject("Serilog", "3.0.1", "Serilog", "3.0.1") },
            new Options());

        var propsOverride = Assert.Single(result);
        Assert.Equal("3.0.1", propsOverride.ProjectVersion.OriginalValue);
        Assert.Equal("3.0.1", propsOverride.DirectoryBuildPropsVersion.OriginalValue);
    }

    [Fact]
    public void An_override_is_detected_when_the_two_package_ids_differ_only_in_case()
    {
        var analyzer = new PackagesAnalyzer();

        var result = analyzer.FindDirectoryBuildPropsOverrides(
            new List<ProjectInfo> { CreateOverridingProject("serilog", "4.0.0", "Serilog", "3.0.1") },
            new Options());

        var propsOverride = Assert.Single(result);

        // The casing reported is the project's own, since that is the declaration being flagged.
        Assert.Equal("serilog", propsOverride.PackageId);
    }

    [Fact]
    public void A_package_only_inherited_or_only_declared_is_not_an_override()
    {
        var analyzer = new PackagesAnalyzer();

        // Serilog only inherited, Moq only declared — neither is overridden by the other.
        var result = analyzer.FindDirectoryBuildPropsOverrides(
            new List<ProjectInfo> { CreateOverridingProject("Moq", "4.18.1", "Serilog", "3.0.1") },
            new Options());

        Assert.Empty(result);
    }

    [Fact]
    public void Overrides_are_filtered_by_the_same_package_id_options_as_the_consolidation_report()
    {
        var analyzer = new PackagesAnalyzer();
        var projectInfos = new List<ProjectInfo>
        {
            CreateOverridingProject("Serilog", "4.0.0", "Serilog", "3.0.1")
        };

        Assert.Single(
            analyzer.FindDirectoryBuildPropsOverrides(
                projectInfos,
                new Options { PackageIds = new List<string> { "serilog" } }));

        Assert.Empty(
            analyzer.FindDirectoryBuildPropsOverrides(
                projectInfos,
                new Options { PackageIds = new List<string> { "Moq" } }));

        Assert.Empty(
            analyzer.FindDirectoryBuildPropsOverrides(
                projectInfos,
                new Options { ExcludedPackageIds = new List<string> { "serilog" } }));
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

    /// <summary>
    /// A project shaped the way <see cref="SolutionInfoProvider"/> leaves one that inherits from a
    /// <c>Directory.Build.props</c>: its own references, plus the props file's appended as
    /// <see cref="NuGetPackageReferenceType.Inherited"/>.
    /// </summary>
    private static ProjectInfo CreateOverridingProject(
        string directPackageId,
        string directVersion,
        string inheritedPackageId,
        string inheritedVersion)
    {
        return new ProjectInfo(
            "ProjectB",
            "ProjectB",
            new List<NuGetPackageInfo>
            {
                new NuGetPackageInfo(directPackageId, new Version(directVersion), NuGetPackageReferenceType.Direct),
                new NuGetPackageInfo(
                    inheritedPackageId,
                    new Version(inheritedVersion),
                    NuGetPackageReferenceType.Inherited)
            })
        {
            DirectoryBuildPropsFile = PropsFile
        };
    }
}
