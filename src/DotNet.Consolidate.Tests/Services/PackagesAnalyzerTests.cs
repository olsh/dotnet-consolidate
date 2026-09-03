using System.Collections.Generic;
using System.Linq;

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
        var result = PackagesAnalyzer.FindNonConsolidatedPackages(projectInfos, options);

        Assert.All(result, analysisResult => Assert.False(analysisResult.ContainsDifferentPackagesVersions));
    }

    [Fact]
    public void Packages_with_different_versions_are_not_consolidated()
    {
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
        var result = PackagesAnalyzer.FindNonConsolidatedPackages(projectInfos, options);

        Assert.All(result, analysisResult => Assert.True(analysisResult.ContainsDifferentPackagesVersions));
    }

    [Theory]
    [InlineData(".*-alpha$", true)]
    [InlineData(".*-beta$", false)]
    [InlineData("", false)]
    public void Packages_version_exclude_regex_correctly_matches(string excludedPackageVersionsRegex, bool shouldMatch)
    {
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
        var result = PackagesAnalyzer.FindNonConsolidatedPackages(projectInfos, options);

        Assert.All(
            result,
            analysisResult => Assert.NotEqual(shouldMatch, analysisResult.ContainsDifferentPackagesVersions));
    }

    [Fact]
    public void Package_ids_given_with_p_are_matched_case_insensitively()
    {
        var options = new Options { PackageIds = new List<string> { "serilog" } };

        var result = PackagesAnalyzer.FindNonConsolidatedPackages(CreateSerilogProjects(), options);

        var analysisResult = Assert.Single(result);
        Assert.Equal("Serilog", analysisResult.NuGetPackageId);
    }

    [Fact]
    public void Package_ids_given_with_e_are_matched_case_insensitively()
    {
        var options = new Options { ExcludedPackageIds = new List<string> { "serilog" } };

        var result = PackagesAnalyzer.FindNonConsolidatedPackages(CreateSerilogProjects(), options);

        Assert.Empty(result);
    }

    [Fact]
    public void Packages_differing_only_in_case_are_the_same_package()
    {
        // NuGet package IDs are case-insensitive, so this is one package at two versions, not two packages
        // each consolidated with itself.
        var projectInfos = new List<ProjectInfo>
        {
            CreateProject("ProjectA", "Serilog", "1.0.0"),
            CreateProject("ProjectB", "serilog", "2.0.0")
        };

        var result = PackagesAnalyzer.FindNonConsolidatedPackages(projectInfos, new Options());

        var analysisResult = Assert.Single(result);
        Assert.Equal(2, analysisResult.PackageVersions.Count);
    }

    [Fact]
    public void Package_ids_present_in_a_different_case_are_not_reported_as_missing()
    {
        var result = PackagesAnalyzer.FindPackageIdsNotInSolution(
            new List<ProjectInfo> { CreateProject("ProjectA", "Serilog", "1.0.0") },
            new List<string> { "serilog" });

        Assert.Empty(result);
    }

    [Fact]
    public void Package_ids_absent_from_the_solution_are_reported_as_missing()
    {
        var result = PackagesAnalyzer.FindPackageIdsNotInSolution(
            new List<ProjectInfo> { CreateProject("ProjectA", "Serilog", "1.0.0") },
            new List<string> { "NotReferenced" });

        // Echoed back as the user typed it.
        Assert.Equal(new[] { "NotReferenced" }, result);
    }

    [Fact]
    public void A_package_declared_by_both_the_project_and_its_props_file_is_an_override()
    {
        var result = PackagesAnalyzer.FindDirectoryBuildPropsOverrides(
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
        var result = PackagesAnalyzer.FindDirectoryBuildPropsOverrides(
            new List<ProjectInfo> { CreateOverridingProject("Serilog", "3.0.1", "Serilog", "3.0.1") },
            new Options());

        var propsOverride = Assert.Single(result);
        Assert.Equal("3.0.1", propsOverride.ProjectVersion.OriginalValue);
        Assert.Equal("3.0.1", propsOverride.DirectoryBuildPropsVersion.OriginalValue);
    }

    [Fact]
    public void An_override_is_detected_when_the_two_package_ids_differ_only_in_case()
    {
        var result = PackagesAnalyzer.FindDirectoryBuildPropsOverrides(
            new List<ProjectInfo> { CreateOverridingProject("serilog", "4.0.0", "Serilog", "3.0.1") },
            new Options());

        var propsOverride = Assert.Single(result);

        // The casing reported is the project's own, since that is the declaration being flagged.
        Assert.Equal("serilog", propsOverride.PackageId);
    }

    [Fact]
    public void A_package_only_inherited_or_only_declared_is_not_an_override()
    {
        // Serilog only inherited, Moq only declared — neither is overridden by the other.
        var result = PackagesAnalyzer.FindDirectoryBuildPropsOverrides(
            new List<ProjectInfo> { CreateOverridingProject("Moq", "4.18.1", "Serilog", "3.0.1") },
            new Options());

        Assert.Empty(result);
    }

    [Fact]
    public void Overrides_are_filtered_by_the_same_package_id_options_as_the_consolidation_report()
    {
        var projectInfos = new List<ProjectInfo>
        {
            CreateOverridingProject("Serilog", "4.0.0", "Serilog", "3.0.1")
        };

        Assert.Single(
            PackagesAnalyzer.FindDirectoryBuildPropsOverrides(
                projectInfos,
                new Options { PackageIds = new List<string> { "serilog" } }));

        Assert.Empty(
            PackagesAnalyzer.FindDirectoryBuildPropsOverrides(
                projectInfos,
                new Options { PackageIds = new List<string> { "Moq" } }));

        Assert.Empty(
            PackagesAnalyzer.FindDirectoryBuildPropsOverrides(
                projectInfos,
                new Options { ExcludedPackageIds = new List<string> { "serilog" } }));
    }

    [Fact]
    public void An_update_replaces_the_inherited_version()
    {
        var project = CreateInheritingProject(update: new PackageVersionUpdate("Serilog", new Version("4.0.0"), true));

        var package = Assert.Single(PackagesAnalyzer.GetEffectivePackages(project));

        // Once, at the updated version — an Update modifies the inherited item instead of adding a second one.
        Assert.Equal("Serilog", package.Id);
        Assert.Equal("4.0.0", package.Version.OriginalValue);
    }

    [Fact]
    public void An_update_that_matches_nothing_adds_nothing()
    {
        var project = CreateInheritingProject(update: new PackageVersionUpdate("Moq", new Version("4.18.1"), true));

        var package = Assert.Single(PackagesAnalyzer.GetEffectivePackages(project));

        Assert.Equal("Serilog", package.Id);
        Assert.Equal("3.0.1", package.Version.OriginalValue);
    }

    [Fact]
    public void An_update_that_is_not_certain_to_apply_keeps_the_inherited_version_beside_it()
    {
        var project = CreateInheritingProject(
            update: new PackageVersionUpdate("Serilog", new Version("4.0.0"), false));

        var versions = PackagesAnalyzer.GetEffectivePackages(project)
            .Select(p => p.Version.OriginalValue);

        Assert.Equal(new[] { "3.0.1", "4.0.0" }, versions);
    }

    [Fact]
    public void An_update_only_reaches_the_inherited_packages()
    {
        // The project's own references were already resolved in document order by the evaluator. Applying an
        // update to them again here would change an include that sits below the update naming it.
        var project = new ProjectInfo(
            "ProjectB",
            "ProjectB",
            new List<NuGetPackageInfo>
            {
                new NuGetPackageInfo("Serilog", new Version("1.0.0"), NuGetPackageReferenceType.Direct)
            },
            new[] { new PackageVersionUpdate("Serilog", new Version("4.0.0"), true) },
            new List<string>());

        var package = Assert.Single(PackagesAnalyzer.GetEffectivePackages(project));
        Assert.Equal("1.0.0", package.Version.OriginalValue);
    }

    [Fact]
    public void A_removal_drops_the_inherited_package()
    {
        var project = CreateInheritingProject(removedPackageId: "serilog");

        Assert.Empty(PackagesAnalyzer.GetEffectivePackages(project));
    }

    [Fact]
    public void A_removal_wins_over_an_update_of_the_same_package()
    {
        var project = CreateInheritingProject(
            update: new PackageVersionUpdate("Serilog", new Version("4.0.0"), true),
            removedPackageId: "Serilog");

        Assert.Empty(PackagesAnalyzer.GetEffectivePackages(project));
    }

    [Fact]
    public void An_updated_package_is_consolidated_against_the_version_it_was_updated_to()
    {
        // The defect behind the change: ProjectB restores 4.0.0, so it does not agree with ProjectA on 3.0.1.
        var projectInfos = new List<ProjectInfo>
        {
            CreateInheritingProject("ProjectA"),
            CreateInheritingProject("ProjectB", new PackageVersionUpdate("Serilog", new Version("4.0.0"), true))
        };

        var result = Assert.Single(PackagesAnalyzer.FindNonConsolidatedPackages(projectInfos, new Options()));

        Assert.Equal("Serilog", result.NuGetPackageId);
        Assert.Equal(
            new[] { "ProjectA 3.0.1", "ProjectB 4.0.0" },
            result.PackageVersions.Select(v => $"{v.ProjectName} {v.NuGetPackageVersion.OriginalValue}"));
    }

    [Fact]
    public void A_package_every_project_removes_is_not_in_the_solution()
    {
        var projectInfos = new List<ProjectInfo> { CreateInheritingProject(removedPackageId: "Serilog") };

        Assert.Equal(
            new[] { "Serilog" },
            PackagesAnalyzer.FindPackageIdsNotInSolution(projectInfos, new[] { "Serilog" }));
    }

    [Fact]
    public void A_package_updated_by_the_project_file_is_an_override()
    {
        var projectInfos = new List<ProjectInfo>
        {
            CreateInheritingProject("ProjectB", new PackageVersionUpdate("Serilog", new Version("4.0.0"), true))
        };

        var propsOverride = Assert.Single(
            PackagesAnalyzer.FindDirectoryBuildPropsOverrides(projectInfos, new Options()));

        Assert.Equal("ProjectB", propsOverride.ProjectName);
        Assert.Equal("Serilog", propsOverride.PackageId);
        Assert.Equal("4.0.0", propsOverride.ProjectVersion.OriginalValue);
        Assert.Equal("3.0.1", propsOverride.DirectoryBuildPropsVersion.OriginalValue);
        Assert.Equal(PropsFile, propsOverride.DirectoryBuildPropsFile);
    }

    [Fact]
    public void An_update_of_a_package_the_project_also_declares_is_reported_once()
    {
        // The evaluator has already applied the update to the declared reference, so both forms would print
        // the very same line.
        var project = CreateOverridingProject("Serilog", "4.0.0", "Serilog", "3.0.1");
        var projectInfos = new List<ProjectInfo>
        {
            new ProjectInfo(
                project.ProjectName,
                project.ProjectDirectory,
                project.Packages,
                new[] { new PackageVersionUpdate("Serilog", new Version("4.0.0"), true) },
                new List<string>())
            {
                DirectoryBuildPropsFile = PropsFile
            }
        };

        Assert.Single(PackagesAnalyzer.FindDirectoryBuildPropsOverrides(projectInfos, new Options()));
    }

    [Fact]
    public void An_update_above_its_own_include_is_reported_beside_the_declared_override()
    {
        // MSBuild applies an update to the items declared before it, so an Update sitting above its own
        // Include never reaches it: the csproj declares 1.0.0 and separately pushes the inherited item to
        // 4.0.0. Both pin the props version, at two different versions, and both belong in the report --
        // skipping the update because the ID happened to be declared hid the second one.
        var projectInfos = new List<ProjectInfo> { CreateUpdateAboveIncludeProject() };

        var reportedVersions = PackagesAnalyzer.FindDirectoryBuildPropsOverrides(projectInfos, new Options())
            .Select(o => $"{o.ProjectVersion.OriginalValue} overrides {o.DirectoryBuildPropsVersion.OriginalValue}")
            .OrderBy(o => o);

        Assert.Equal(new[] { "1.0.0 overrides 3.0.1", "4.0.0 overrides 3.0.1" }, reportedVersions);
    }

    [Fact]
    public void An_update_above_its_own_include_leaves_both_versions_referenced()
    {
        // The counterpart of the report above: this is what the project actually restores.
        var versions = PackagesAnalyzer.GetEffectivePackages(CreateUpdateAboveIncludeProject())
            .Select(p => p.Version.OriginalValue)
            .OrderBy(v => v);

        Assert.Equal(new[] { "1.0.0", "4.0.0" }, versions);
    }

    [Fact]
    public void A_removed_package_is_not_an_override()
    {
        var projectInfos = new List<ProjectInfo> { CreateInheritingProject(removedPackageId: "Serilog") };

        Assert.Empty(PackagesAnalyzer.FindDirectoryBuildPropsOverrides(projectInfos, new Options()));
    }

    [Fact]
    public void Update_overrides_are_filtered_by_the_same_package_id_options_as_the_consolidation_report()
    {
        var projectInfos = new List<ProjectInfo>
        {
            CreateInheritingProject("ProjectB", new PackageVersionUpdate("Serilog", new Version("4.0.0"), true))
        };

        Assert.Single(
            PackagesAnalyzer.FindDirectoryBuildPropsOverrides(
                projectInfos,
                new Options { PackageIds = new List<string> { "serilog" } }));

        Assert.Empty(
            PackagesAnalyzer.FindDirectoryBuildPropsOverrides(
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
    /// A project shaped the way <c>&lt;PackageReference Update="Serilog" Version="4.0.0" /&gt;</c> above its
    /// own <c>&lt;PackageReference Include="Serilog" Version="1.0.0" /&gt;</c> leaves one, inheriting
    /// <c>Serilog 3.0.1</c>: the evaluator kept the declared 1.0.0, because an update reaches only the items
    /// above it, and carried the update out for the inherited entry.
    /// </summary>
    private static ProjectInfo CreateUpdateAboveIncludeProject()
    {
        return new ProjectInfo(
            "ProjectB",
            "ProjectB",
            new List<NuGetPackageInfo>
            {
                new NuGetPackageInfo("Serilog", new Version("1.0.0"), NuGetPackageReferenceType.Direct),
                new NuGetPackageInfo("Serilog", new Version("3.0.1"), NuGetPackageReferenceType.Inherited)
            },
            new[] { new PackageVersionUpdate("Serilog", new Version("4.0.0"), true) },
            new List<string>())
        {
            DirectoryBuildPropsFile = PropsFile
        };
    }

    /// <summary>
    /// A project that declares nothing of its own and inherits <c>Serilog 3.0.1</c> from its props file,
    /// optionally updating or removing it the way a <c>&lt;PackageReference Update="…" /&gt;</c> would.
    /// </summary>
    private static ProjectInfo CreateInheritingProject(
        string projectName = "ProjectB",
        PackageVersionUpdate update = null,
        string removedPackageId = null)
    {
        return new ProjectInfo(
            projectName,
            projectName,
            new List<NuGetPackageInfo>
            {
                new NuGetPackageInfo("Serilog", new Version("3.0.1"), NuGetPackageReferenceType.Inherited)
            },
            update == null ? new List<PackageVersionUpdate>() : new List<PackageVersionUpdate> { update },
            removedPackageId == null ? new List<string>() : new List<string> { removedPackageId })
        {
            DirectoryBuildPropsFile = PropsFile
        };
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
