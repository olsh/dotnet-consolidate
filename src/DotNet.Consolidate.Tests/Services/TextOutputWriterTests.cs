using System.Collections.Generic;
using System.IO;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;

using Xunit;

namespace DotNet.Consolidate.Tests.Services;

public class TextOutputWriterTests
{
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
        Assert.Contains($"ProjectA - 7.0.1{System.Environment.NewLine}ProjectB - 7.0.2", text);
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
                new List<string>()));
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
                new List<string> { "NotReferenced" }));
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
                new List<string>()));
        writer.Flush();

        var text = output.ToString();
        Assert.DoesNotContain(
            "The following package IDs given for consolidation check were not found in the solution projects:",
            text);
        Assert.Contains("All packages from the list Serilog in My.sln are consolidated.", text);
    }

    private static SolutionAnalysisResult CreateResult(string solutionFile, params AnalysisResult[] packages)
    {
        return new SolutionAnalysisResult(
            solutionFile,
            isParsedWithoutIssues: true,
            packages,
            new List<string>(),
            new List<string>());
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
