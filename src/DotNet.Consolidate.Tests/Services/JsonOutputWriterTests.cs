using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;

using Xunit;

namespace DotNet.Consolidate.Tests.Services;

public class JsonOutputWriterTests
{
    [Fact]
    public void Non_consolidated_packages_are_written_per_solution()
    {
        var output = new StringWriter();
        var writer = new JsonOutputWriter(output, new List<string>());

        writer.WriteAnalysisResults(
            CreateResult(
                "My.sln",
                CreatePackage("System.Text.Json", ("ProjectA", "7.0.1"), ("ProjectB", "7.0.2"))));
        writer.Flush();

        using var document = JsonDocument.Parse(output.ToString());
        var solution = Assert.Single(
            document.RootElement.GetProperty("solutions")
                .EnumerateArray());
        Assert.Equal(
            "My.sln",
            solution.GetProperty("solutionFile")
                .GetString());
        Assert.True(
            solution.GetProperty("isParsedWithoutIssues")
                .GetBoolean());

        var package = Assert.Single(
            solution.GetProperty("nonConsolidatedPackages")
                .EnumerateArray());
        Assert.Equal(
            "System.Text.Json",
            package.GetProperty("packageId")
                .GetString());
        Assert.Equal(
            new[] { "ProjectA - 7.0.1", "ProjectB - 7.0.2" },
            package.GetProperty("packageVersions")
                .EnumerateArray()
                .Select(v => $"{v.GetProperty("projectName").GetString()} - {v.GetProperty("version").GetString()}"));
    }

    [Fact]
    public void Property_names_are_camel_cased()
    {
        var output = new StringWriter();
        var writer = new JsonOutputWriter(output, new List<string>());

        writer.WriteAnalysisResults(CreateResult("My.sln", CreatePackage("Serilog", ("ProjectA", "3.0.1"))));
        writer.Flush();

        var json = output.ToString();
        Assert.Contains("\"nonConsolidatedPackages\"", json);
        Assert.DoesNotContain("\"NonConsolidatedPackages\"", json);
    }

    [Fact]
    public void Several_solutions_are_written_as_a_single_document()
    {
        var output = new StringWriter();
        var writer = new JsonOutputWriter(output, new List<string>());

        writer.WriteAnalysisResults(CreateResult("First.sln", CreatePackage("Serilog", ("ProjectA", "3.0.1"))));
        writer.WriteAnalysisResults(CreateResult("Second.sln", CreatePackage("Moq", ("ProjectB", "4.18.1"))));
        writer.Flush();

        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal(
            new[] { "First.sln", "Second.sln" },
            document.RootElement.GetProperty("solutions")
                .EnumerateArray()
                .Select(s => s.GetProperty("solutionFile")
                    .GetString()));
    }

    [Fact]
    public void Logged_messages_are_written_as_warnings_and_progress_is_dropped()
    {
        var output = new StringWriter();
        var logger = new CollectingLogger();
        var writer = new JsonOutputWriter(output, logger.Messages);

        // Logged after the writer is created, the way a run logs while the solutions are being analyzed.
        logger.Progress("Analyzing packages in My.sln");
        logger.Message("Unable to parse ProjectA.csproj");

        writer.WriteAnalysisResults(CreateResult("My.sln"));
        writer.Flush();

        using var document = JsonDocument.Parse(output.ToString());
        var warning = Assert.Single(
            document.RootElement.GetProperty("warnings")
                .EnumerateArray());
        Assert.Equal("Unable to parse ProjectA.csproj", warning.GetString());
    }

    [Fact]
    public void Consolidated_solution_is_written_with_an_empty_package_list()
    {
        var output = new StringWriter();
        var writer = new JsonOutputWriter(output, new List<string>());

        writer.WriteAnalysisResults(CreateResult("My.sln"));
        writer.Flush();

        using var document = JsonDocument.Parse(output.ToString());
        var solution = Assert.Single(
            document.RootElement.GetProperty("solutions")
                .EnumerateArray());
        Assert.Empty(
            solution.GetProperty("nonConsolidatedPackages")
                .EnumerateArray());
    }

    [Fact]
    public void Package_versions_are_ordered_by_version_then_project_name()
    {
        var output = new StringWriter();
        var writer = new JsonOutputWriter(output, new List<string>());

        writer.WriteAnalysisResults(
            CreateResult(
                "My.sln",
                CreatePackage(
                    "System.Text.Json",
                    ("ProjectC", "7.0.2"),
                    ("ProjectB", "7.0.1"),
                    ("ProjectA", "7.0.1"))));
        writer.Flush();

        using var document = JsonDocument.Parse(output.ToString());
        var package = Assert.Single(
            document.RootElement.GetProperty("solutions")
                .EnumerateArray()
                .Single()
                .GetProperty("nonConsolidatedPackages")
                .EnumerateArray());

        Assert.Equal(
            new[] { "ProjectA", "ProjectB", "ProjectC" },
            package.GetProperty("packageVersions")
                .EnumerateArray()
                .Select(v => v.GetProperty("projectName")
                    .GetString()));
    }

    [Fact]
    public void Solution_that_was_not_parsed_correctly_is_flagged()
    {
        var output = new StringWriter();
        var writer = new JsonOutputWriter(output, new List<string>());

        writer.WriteAnalysisResults(
            new SolutionAnalysisResult(
                "My.sln",
                isParsedWithoutIssues: false,
                new List<AnalysisResult>(),
                new List<string>(),
                new List<string>()));
        writer.Flush();

        using var document = JsonDocument.Parse(output.ToString());
        var solution = Assert.Single(
            document.RootElement.GetProperty("solutions")
                .EnumerateArray());
        Assert.False(
            solution.GetProperty("isParsedWithoutIssues")
                .GetBoolean());
    }

    [Fact]
    public void Requested_package_ids_that_are_missing_from_the_solution_are_written()
    {
        var output = new StringWriter();
        var writer = new JsonOutputWriter(output, new List<string>());

        writer.WriteAnalysisResults(
            new SolutionAnalysisResult(
                "My.sln",
                isParsedWithoutIssues: true,
                new List<AnalysisResult>(),
                new List<string> { "Serilog", "NotReferenced" },
                new List<string> { "NotReferenced" }));
        writer.Flush();

        using var document = JsonDocument.Parse(output.ToString());
        var solution = Assert.Single(
            document.RootElement.GetProperty("solutions")
                .EnumerateArray());
        var missing = Assert.Single(
            solution.GetProperty("packageIdsNotFound")
                .EnumerateArray());
        Assert.Equal("NotReferenced", missing.GetString());
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
