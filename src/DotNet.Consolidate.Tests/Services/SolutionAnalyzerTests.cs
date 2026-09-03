using System.Collections.Generic;
using System.IO;
using System.Linq;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;
using DotNet.Consolidate.Tests.Helpers;

using Xunit;

using Version = DotNet.Consolidate.Models.Version;

namespace DotNet.Consolidate.Tests.Services;

/// <summary>
/// Covers the pooled analysis behind <c>-c</c>, and the per-solution one it shares its rules with.
/// </summary>
public class SolutionAnalyzerTests
{
    private static readonly string PropsFile = Dir("cms", "Directory.Build.props");

    [Fact]
    public void Projects_from_every_solution_are_analyzed_together()
    {
        // The point of the flag: neither solution disagrees with itself, so on its own each one is consolidated.
        var result = AnalyzeAcross(
            ("cms.sln", CreateProject("ProjectA", Dir("cms", "ProjectA"), "Serilog", "1.0.0")),
            ("TheSite.sln", CreateProject("ProjectB", Dir("site", "ProjectB"), "Serilog", "2.0.0")));

        var package = Assert.Single(result.NonConsolidatedPackages);
        Assert.Equal("Serilog", package.NuGetPackageId);
        Assert.Equal(
            new[] { "ProjectA - 1.0.0", "ProjectB - 2.0.0" },
            package.PackageVersions.OrderBy(p => p.ProjectName)
                .Select(p => $"{p.ProjectName} - {p.NuGetPackageVersion.OriginalValue}"));
    }

    [Fact]
    public void Each_solution_on_its_own_reports_nothing()
    {
        // The contrast that shows the flag is what does the work above.
        var options = new Options();

        Assert.Empty(
            SolutionAnalyzer.Analyze(
                    new[] { "cms.sln" },
                    isParsedWithoutIssues: true,
                    new List<ProjectInfo>
                    {
                        CreateProject("ProjectA", Dir("cms", "ProjectA"), "Serilog", "1.0.0")
                    },
                    options)
                .NonConsolidatedPackages);
        Assert.Empty(
            SolutionAnalyzer.Analyze(
                    new[] { "TheSite.sln" },
                    isParsedWithoutIssues: true,
                    new List<ProjectInfo>
                    {
                        CreateProject("ProjectB", Dir("site", "ProjectB"), "Serilog", "2.0.0")
                    },
                    options)
                .NonConsolidatedPackages);
    }

    [Fact]
    public void The_report_names_every_solution_it_covers()
    {
        var result = AnalyzeAcross(
            ("cms.sln", CreateProject("ProjectA", Dir("cms", "ProjectA"), "Serilog", "1.0.0")),
            ("TheSite.sln", CreateProject("ProjectB", Dir("site", "ProjectB"), "Serilog", "1.0.0")));

        Assert.Equal(new[] { "cms.sln", "TheSite.sln" }, result.SolutionFiles);
        Assert.Equal("cms.sln, TheSite.sln", result.SolutionFile);
    }

    [Fact]
    public void A_single_solution_still_reports_itself_as_one_file()
    {
        var result = AnalyzeAcross(("cms.sln", CreateProject("ProjectA", Dir("cms", "ProjectA"), "Serilog", "1.0.0")));

        Assert.Equal(new[] { "cms.sln" }, result.SolutionFiles);
        Assert.Equal("cms.sln", result.SolutionFile);
    }

    [Fact]
    public void A_solution_that_was_not_parsed_correctly_makes_the_whole_report_untrustworthy()
    {
        Assert.False(
            SolutionAnalyzer.Analyze(
                    new[] { "cms.sln", "TheSite.sln" },
                    isParsedWithoutIssues: false,
                    new List<ProjectInfo>(),
                    new Options())
                .IsParsedWithoutIssues);
    }

    [Fact]
    public void A_project_shared_by_two_solutions_is_analyzed_once()
    {
        var result = AnalyzeAcross(
            ("cms.sln", CreateProject("Shared", Dir("cms", "Shared"), "Serilog", "1.0.0")),
            ("TheSite.sln", CreateProject("Shared", Dir("cms", "Shared"), "Serilog", "1.0.0")),
            ("third.sln", CreateProject("ProjectB", Dir("site", "ProjectB"), "Serilog", "2.0.0")));

        var package = Assert.Single(result.NonConsolidatedPackages);
        Assert.Equal(
            new[] { "ProjectB", "Shared" },
            package.PackageVersions.Select(p => p.ProjectName)
                .OrderBy(name => name));
    }

    [Fact]
    public void A_project_shared_by_two_solutions_does_not_invent_a_discrepancy()
    {
        // Each solution walks for Directory.Build.props from its own directory, so the copy the second solution
        // read inherits nothing. Keeping both would report a version difference no build actually has.
        var result = AnalyzeAcross(
            ("cms.sln", CreateInheritingProject("Shared", Dir("cms", "Shared"), "3.0.1")),
            ("TheSite.sln", CreateProject("Shared", Dir("cms", "Shared"), "Serilog", "1.0.0")));

        Assert.Empty(result.NonConsolidatedPackages);
    }

    [Fact]
    public void The_first_solution_on_the_command_line_wins_for_a_shared_project()
    {
        var pooled = SolutionAnalyzer.PoolProjects(
            new[]
            {
                new List<ProjectInfo> { CreateInheritingProject("Shared", Dir("cms", "Shared"), "3.0.1") },
                new List<ProjectInfo> { CreateProject("Shared", Dir("cms", "Shared"), "Serilog", "1.0.0") }
            });

        var project = Assert.Single(pooled);
        Assert.Equal(PropsFile, project.DirectoryBuildPropsFile);
        Assert.Equal(
            "3.0.1",
            Assert.Single(project.Packages)
                .Version.OriginalValue);
    }

    [Fact]
    public void Two_spellings_of_the_same_project_path_are_the_same_project()
    {
        // A solution can reach a shared project through `..`, which is not string-equal to the direct path.
        var pooled = SolutionAnalyzer.PoolProjects(
            new[]
            {
                new List<ProjectInfo> { CreateProject("Shared", Dir("cms", "Shared"), "Serilog", "1.0.0") },
                new List<ProjectInfo>
                {
                    CreateProject("Shared", Dir("cms", "ProjectA", "..", "Shared"), "Serilog", "1.0.0")
                }
            });

        Assert.Single(pooled);
    }

    [Fact]
    public void Projects_with_the_same_name_in_different_directories_are_both_analyzed()
    {
        var result = AnalyzeAcross(
            ("cms.sln", CreateProject("Shared", Dir("cms", "Shared"), "Serilog", "1.0.0")),
            ("TheSite.sln", CreateProject("Shared", Dir("site", "Shared"), "Serilog", "2.0.0")));

        Assert.Equal(
            2,
            Assert.Single(result.NonConsolidatedPackages)
                .PackageVersions.Count);
    }

    [Fact]
    public void Two_projects_in_one_directory_are_both_analyzed()
    {
        // Same directory, same display name, different project files — merging them would drop a reference that
        // is really there, which is why the project file is the identity.
        var pooled = SolutionAnalyzer.PoolProjects(
            new[]
            {
                new List<ProjectInfo>
                {
                    CreateProject(
                        "Shared",
                        Dir("cms", "Shared"),
                        "Serilog",
                        "1.0.0",
                        Dir("cms", "Shared", "A.csproj")),
                    CreateProject(
                        "Shared",
                        Dir("cms", "Shared"),
                        "Serilog",
                        "2.0.0",
                        Dir("cms", "Shared", "B.vbproj"))
                }
            });

        Assert.Equal(2, pooled.Count);
    }

    [Fact]
    public void Package_ids_that_no_solution_references_are_reported_once()
    {
        // Serilog is referenced by only one of the two, and pooled that is enough — reporting it as missing is
        // what the flag exists to stop.
        var result = AnalyzeAcross(
            new Options { PackageIds = new List<string> { "Serilog", "NotReferenced" } },
            ("cms.sln", CreateProject("ProjectA", Dir("cms", "ProjectA"), "Serilog", "1.0.0")),
            ("TheSite.sln", CreateProject("ProjectB", Dir("site", "ProjectB"), "Moq", "4.18.1")));

        Assert.Equal(new[] { "NotReferenced" }, result.PackageIdsNotFoundInSolution);
    }

    [Fact]
    public void Directory_build_props_overrides_are_collected_from_every_solution()
    {
        var result = AnalyzeAcross(
            ("cms.sln", CreateOverridingProject("ProjectA", Dir("cms", "ProjectA"))),
            ("TheSite.sln", CreateOverridingProject("ProjectB", Dir("site", "ProjectB"))));

        Assert.Equal(
            new[] { "ProjectA", "ProjectB" },
            result.DirectoryBuildPropsOverrides.Select(o => o.ProjectName)
                .OrderBy(name => name));
    }

    [Fact]
    public void Directory_build_props_overrides_are_not_reported_twice_for_a_shared_project()
    {
        var result = AnalyzeAcross(
            ("cms.sln", CreateOverridingProject("Shared", Dir("cms", "Shared"))),
            ("TheSite.sln", CreateOverridingProject("Shared", Dir("cms", "Shared"))));

        Assert.Single(result.DirectoryBuildPropsOverrides);
    }

    [Fact]
    public void Overrides_are_not_collected_when_reporting_them_is_off()
    {
        var result = AnalyzeAcross(
            new Options { ReportOverridenDirectoryBuildProps = false },
            ("cms.sln", CreateOverridingProject("ProjectA", Dir("cms", "ProjectA"))));

        Assert.Empty(result.DirectoryBuildPropsOverrides);
    }

    [Fact]
    public void The_requested_package_ids_are_carried_into_the_report()
    {
        var result = AnalyzeAcross(
            new Options { PackageIds = new List<string> { "Serilog" } },
            ("cms.sln", CreateProject("ProjectA", Dir("cms", "ProjectA"), "Serilog", "1.0.0")));

        Assert.Equal(new[] { "Serilog" }, result.RequestedPackageIds);
    }

    [Fact]
    public void Pooling_the_same_solution_twice_reports_what_it_reports_on_its_own()
    {
        // The two serializations of the sample solution describe the same tree, so every project is shared and
        // the pooled report has to come out identical to the single-solution one. This is the de-duplication
        // running against what SolutionInfoProvider really produces, rather than hand-built projects.
        var options = new Options();

        Assert.Equal(
            Describe(SolutionAnalyzer.AnalyzeAcrossSolutions(ParseTestSolutions("TestSolution.sln"), options)),
            Describe(
                SolutionAnalyzer.AnalyzeAcrossSolutions(
                    ParseTestSolutions("TestSolution.sln", "TestSolution.slnx"),
                    options)));
    }

    [Fact]
    public void Pooling_two_solutions_covers_the_projects_of_both()
    {
        var solutionInfos = ParseTestSolutions("TestSolution.sln", "TestSolution.slnx");

        var result = SolutionAnalyzer.AnalyzeAcrossSolutions(solutionInfos, new Options());

        Assert.Equal(
            solutionInfos.Select(solutionInfo => solutionInfo.SolutionFile),
            result.SolutionFiles);
        Assert.True(result.IsParsedWithoutIssues);

        // Both readings of every project collapsed back to one.
        Assert.Equal(
            solutionInfos.First()
                .ProjectInfos.Count,
            SolutionAnalyzer.PoolProjects(solutionInfos.Select(solutionInfo => solutionInfo.ProjectInfos))
                .Count);
    }

    private static List<SolutionInfo> ParseTestSolutions(params string[] solutionFileNames)
    {
        var solutionInfoProvider = new SolutionInfoProvider(new ProjectParser(new Logger()), new Logger(), true);

        return solutionInfoProvider.GetSolutionsInfo(
            solutionFileNames.Select(FileHelper.TestSolutionFile)
                .ToList());
    }

    /// <summary>
    /// The findings of a report, flattened to what two runs must agree on.
    /// </summary>
    private static IEnumerable<string> Describe(SolutionAnalysisResult result)
    {
        return result.NonConsolidatedPackages
            .SelectMany(
                package => package.PackageVersions,
                (package, version) =>
                    $"{package.NuGetPackageId} {version.ProjectName} {version.NuGetPackageVersion.OriginalValue}")
            .OrderBy(line => line)
            .ToList();
    }

    /// <remarks>
    /// Built with <see cref="Path.Combine(string[])"/> rather than written out: the build runs on Linux too,
    /// where a backslash is an ordinary character, so a hand-written Windows path would leave a <c>..</c>
    /// uncollapsed and quietly stop the resolution tests from testing anything.
    /// </remarks>
    private static string Dir(params string[] parts)
    {
        return Path.Combine(parts);
    }

    private static SolutionAnalysisResult AnalyzeAcross(params (string SolutionFile, ProjectInfo Project)[] solutions)
    {
        return AnalyzeAcross(new Options(), solutions);
    }

    private static SolutionAnalysisResult AnalyzeAcross(
        Options options,
        params (string SolutionFile, ProjectInfo Project)[] solutions)
    {
        return SolutionAnalyzer.Analyze(
            solutions.Select(solution => solution.SolutionFile)
                .ToList(),
            isParsedWithoutIssues: true,
            SolutionAnalyzer.PoolProjects(
                solutions.Select(solution => new List<ProjectInfo> { solution.Project })),
            options);
    }

    private static ProjectInfo CreateProject(
        string projectName,
        string projectDirectory,
        string packageId,
        string version,
        string projectFile = null)
    {
        return new ProjectInfo(
            projectName,
            projectDirectory,
            new List<NuGetPackageInfo>
            {
                new NuGetPackageInfo(packageId, new Version(version), NuGetPackageReferenceType.Direct)
            })
        {
            ProjectFile = projectFile ?? Path.Combine(projectDirectory, $"{projectName}.csproj")
        };
    }

    /// <summary>
    /// A project that declares nothing of its own and inherits Serilog from its <c>Directory.Build.props</c>,
    /// the way <see cref="SolutionInfoProvider"/> leaves one.
    /// </summary>
    private static ProjectInfo CreateInheritingProject(string projectName, string projectDirectory, string version)
    {
        return new ProjectInfo(
            projectName,
            projectDirectory,
            new List<NuGetPackageInfo>
            {
                new NuGetPackageInfo("Serilog", new Version(version), NuGetPackageReferenceType.Inherited)
            })
        {
            ProjectFile = Path.Combine(projectDirectory, $"{projectName}.csproj"),
            DirectoryBuildPropsFile = PropsFile
        };
    }

    /// <summary>
    /// A project that re-declares the package its props file already declares, which is what <c>-o</c> reports.
    /// </summary>
    private static ProjectInfo CreateOverridingProject(string projectName, string projectDirectory)
    {
        return new ProjectInfo(
            projectName,
            projectDirectory,
            new List<NuGetPackageInfo>
            {
                new NuGetPackageInfo("Serilog", new Version("4.0.0"), NuGetPackageReferenceType.Direct),
                new NuGetPackageInfo("Serilog", new Version("3.0.1"), NuGetPackageReferenceType.Inherited)
            })
        {
            ProjectFile = Path.Combine(projectDirectory, $"{projectName}.csproj"),
            DirectoryBuildPropsFile = PropsFile
        };
    }
}
