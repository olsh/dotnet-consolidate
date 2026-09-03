using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;

using Xunit;

using Version = DotNet.Consolidate.Models.Version;

namespace DotNet.Consolidate.Tests.Services;

public class TextOutputWriterTests
{
    private const string PropsFile = @"C:\src\Directory.Build.props";

    [Fact]
    public void Non_consolidated_packages_are_listed_under_a_header()
    {
        var output = new StringWriter();
        var writer = new TextOutputWriter(output);

        writer.WriteAnalysisResults(
            CreateResult(
                "My.sln",
                CreatePackage("System.Text.Json", ("ProjectB", "7.0.2"), ("ProjectA", "7.0.1"))));
        writer.Flush();

        var text = output.ToString();
        Assert.Contains("Found 1 non-consolidated packages", text);
        Assert.Contains("----------------------------", text);
        Assert.Contains("System.Text.Json", text);

        // Ordered by version, then by project name.
        Assert.Contains($"ProjectA - 7.0.1{Environment.NewLine}ProjectB - 7.0.2", text);
    }

    [Fact]
    public void Consolidated_solution_reports_that_all_packages_are_consolidated()
    {
        var output = new StringWriter();
        var writer = new TextOutputWriter(output);

        writer.WriteAnalysisResults(CreateResult("My.sln"));
        writer.Flush();

        var text = output.ToString();
        Assert.Contains("All packages in My.sln are consolidated.", text);
        Assert.DoesNotContain("non-consolidated packages", text);
    }

    [Fact]
    public void Every_solution_the_report_covers_is_named_in_the_consolidated_message()
    {
        var output = new StringWriter();
        var writer = new TextOutputWriter(output);

        // What `-c` produces: one report for the whole set rather than one per solution.
        writer.WriteAnalysisResults(
            new SolutionAnalysisResult(
                new[] { "cms.sln", "TheSite.sln" },
                isParsedWithoutIssues: true,
                new List<AnalysisResult>(),
                new List<string>(),
                new List<string>(),
                new List<DirectoryBuildPropsOverride>()));
        writer.Flush();

        Assert.Contains("All packages in cms.sln, TheSite.sln are consolidated.", output.ToString());
    }

    [Fact]
    public void Requested_package_ids_are_named_in_the_consolidated_message()
    {
        var output = new StringWriter();
        var writer = new TextOutputWriter(output);

        writer.WriteAnalysisResults(
            new SolutionAnalysisResult(
                "My.sln",
                isParsedWithoutIssues: true,
                new List<AnalysisResult>(),
                new List<string> { "Serilog" },
                new List<string>(),
                new List<DirectoryBuildPropsOverride>()));
        writer.Flush();

        Assert.Contains("All packages from the list Serilog in My.sln are consolidated.", output.ToString());
    }

    [Fact]
    public void Requested_package_ids_missing_from_the_solution_are_reported()
    {
        var output = new StringWriter();
        var writer = new TextOutputWriter(output);

        writer.WriteAnalysisResults(
            new SolutionAnalysisResult(
                "My.sln",
                isParsedWithoutIssues: true,
                new List<AnalysisResult>(),
                new List<string> { "Serilog", "NotReferenced" },
                new List<string> { "NotReferenced" },
                new List<DirectoryBuildPropsOverride>()));
        writer.Flush();

        var text = output.ToString();
        Assert.Contains(
            "The following package IDs given for consolidation check were not found in the solution projects:",
            text);
        Assert.Contains("NotReferenced", text);

        // A missing package isn't a consolidated one, so the report must not claim otherwise.
        Assert.DoesNotContain("are consolidated.", text);
    }

    [Fact]
    public void Missing_package_ids_header_is_not_printed_when_nothing_is_missing()
    {
        var output = new StringWriter();
        var writer = new TextOutputWriter(output);

        writer.WriteAnalysisResults(
            new SolutionAnalysisResult(
                "My.sln",
                isParsedWithoutIssues: true,
                new List<AnalysisResult>(),
                new List<string> { "Serilog" },
                new List<string>(),
                new List<DirectoryBuildPropsOverride>()));
        writer.Flush();

        var text = output.ToString();
        Assert.DoesNotContain(
            "The following package IDs given for consolidation check were not found in the solution projects:",
            text);
        Assert.Contains("All packages from the list Serilog in My.sln are consolidated.", text);
    }

    [Fact]
    public void Directory_build_props_overrides_are_listed_with_both_versions_and_the_props_file()
    {
        var output = new StringWriter();
        var writer = new TextOutputWriter(output);

        writer.WriteAnalysisResults(
            CreateResultWithOverrides(
                "My.sln",
                CreateOverride("ProjectB", "Serilog", "4.0.0", "3.0.1")));
        writer.Flush();

        var text = output.ToString();
        Assert.Contains("Found 1 Directory.Build.props overrides", text);
        Assert.Contains("Serilog", text);
        Assert.Contains($"ProjectB - 4.0.0 overrides 3.0.1 from {PropsFile}", text);
    }

    [Fact]
    public void Directory_build_props_overrides_are_grouped_by_package_and_ordered_by_project()
    {
        var output = new StringWriter();
        var writer = new TextOutputWriter(output);

        writer.WriteAnalysisResults(
            CreateResultWithOverrides(
                "My.sln",
                CreateOverride("ProjectB", "Serilog", "4.0.0", "3.0.1"),
                CreateOverride("ProjectA", "Serilog", "5.0.0", "3.0.1")));
        writer.Flush();

        var text = output.ToString();
        Assert.Contains("Found 2 Directory.Build.props overrides", text);

        // One header for the package, projects in name order underneath it.
        // `\r?` because in multiline mode `$` matches before the `\n` only, leaving the `\r` of a CRLF break.
        Assert.Single(Regex.Matches(text, @"^Serilog\r?$", RegexOptions.Multiline));
        Assert.True(
            text.IndexOf("ProjectA - 5.0.0", StringComparison.Ordinal)
            < text.IndexOf("ProjectB - 4.0.0", StringComparison.Ordinal));
    }

    [Fact]
    public void Nothing_is_printed_when_there_are_no_directory_build_props_overrides()
    {
        var output = new StringWriter();
        var writer = new TextOutputWriter(output);

        writer.WriteAnalysisResults(CreateResult("My.sln"));
        writer.Flush();

        Assert.DoesNotContain("Directory.Build.props", output.ToString());
    }

    [Fact]
    public void An_override_does_not_stop_a_consolidated_solution_from_being_reported_as_one()
    {
        var output = new StringWriter();
        var writer = new TextOutputWriter(output);

        // An override says nothing about whether the versions agree across projects, so the consolidation
        // verdict stands on its own.
        writer.WriteAnalysisResults(
            CreateResultWithOverrides("My.sln", CreateOverride("ProjectB", "Serilog", "3.0.1", "3.0.1")));
        writer.Flush();

        var text = output.ToString();
        Assert.Contains("ProjectB - 3.0.1 overrides 3.0.1", text);
        Assert.Contains("All packages in My.sln are consolidated.", text);
    }

    private static SolutionAnalysisResult CreateResult(string solutionFile, params AnalysisResult[] packages)
    {
        return new SolutionAnalysisResult(
            solutionFile,
            isParsedWithoutIssues: true,
            packages,
            new List<string>(),
            new List<string>(),
            new List<DirectoryBuildPropsOverride>());
    }

    private static SolutionAnalysisResult CreateResultWithOverrides(
        string solutionFile,
        params DirectoryBuildPropsOverride[] overrides)
    {
        return new SolutionAnalysisResult(
            solutionFile,
            isParsedWithoutIssues: true,
            new List<AnalysisResult>(),
            new List<string>(),
            new List<string>(),
            overrides);
    }

    private static DirectoryBuildPropsOverride CreateOverride(
        string projectName,
        string packageId,
        string projectVersion,
        string directoryBuildPropsVersion,
        string directoryBuildPropsFile = PropsFile)
    {
        return new DirectoryBuildPropsOverride(
            projectName,
            packageId,
            new Version(projectVersion),
            new Version(directoryBuildPropsVersion),
            directoryBuildPropsFile);
    }

    private static AnalysisResult CreatePackage(
        string packageId,
        params (string ProjectName, string Version)[] versions)
    {
        var result = new AnalysisResult(packageId);
        foreach (var (projectName, version) in versions)
        {
            result.PackageVersions.Add(new ProjectNuGetPackageVersion(projectName, new Version(version)));
        }

        return result;
    }
}
