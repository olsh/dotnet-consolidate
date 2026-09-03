using CommandLine;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;

using Xunit;

namespace DotNet.Consolidate.Tests.Services;

/// <summary>
/// Guards the CommandLineParser binding, which nothing else exercises: the options are filled by reflection, so
/// a change to how the properties are declared can silently stop an option from being applied.
/// </summary>
public class OptionsTests
{
    [Fact]
    public void Every_option_is_bound_from_the_command_line()
    {
        var options = Parse(
            "-s",
            "First.sln",
            "Second.sln",
            "-p",
            "Serilog",
            "-e",
            "Moq",
            "--excludedVersionsRegex",
            ".*-alpha$",
            "--property",
            "NuGetBuild=true",
            "-f",
            "json");

        Assert.Equal(new[] { "First.sln", "Second.sln" }, options.Solutions);
        Assert.Equal(new[] { "Serilog" }, options.PackageIds);
        Assert.Equal(new[] { "Moq" }, options.ExcludedPackageIds);
        Assert.Equal(".*-alpha$", options.ExcludedPackageVersionsRegex);
        Assert.Equal(new[] { "NuGetBuild=true" }, options.GlobalProperties);
        Assert.Equal(OutputFormat.Json, options.Format);
    }

    [Theory]
    [InlineData("json", OutputFormat.Json)]
    [InlineData("Json", OutputFormat.Json)]
    [InlineData("JSON", OutputFormat.Json)]
    [InlineData("text", OutputFormat.Text)]
    [InlineData("Text", OutputFormat.Text)]
    public void Format_is_parsed_regardless_of_casing(string value, OutputFormat expected)
    {
        Assert.Equal(
            expected,
            Parse("-s", "My.sln", "-f", value)
                .Format);
    }

    [Fact]
    public void An_unknown_format_is_rejected()
    {
        using var parser = CommandLineParserFactory.Create();

        Assert.IsType<NotParsed<Options>>(parser.ParseArguments<Options>(new[] { "-s", "My.sln", "-f", "xml" }));
    }

    [Theory]
    [InlineData("-d", "false", false)]
    [InlineData("-d", "true", true)]
    [InlineData("--directoryBuildProps", "false", false)]
    [InlineData("--directoryBuildProps", "true", true)]
    public void Directory_build_props_can_be_switched_off(string name, string value, bool expected)
    {
        Assert.Equal(
            expected,
            Parse("-s", "My.sln", name, value)
                .ReadDirectoryBuildProps);
    }

    [Theory]
    [InlineData("-o", "false", false)]
    [InlineData("-o", "true", true)]
    [InlineData("--reportOverridenDirectoryBuildProps", "false", false)]
    [InlineData("--reportOverridenDirectoryBuildProps", "true", true)]
    public void Reporting_overriden_directory_build_props_can_be_switched_off(string name, string value, bool expected)
    {
        Assert.Equal(
            expected,
            Parse("-s", "My.sln", name, value)
                .ReportOverridenDirectoryBuildProps);
    }

    [Fact]
    public void A_toggle_passed_without_a_value_still_switches_it_on()
    {
        // Making the toggles nullable doesn't take the bare `-d` / `-o` form away, so a command line written
        // against the old switch behaviour keeps working.
        Assert.Equal(
            true,
            Parse("-s", "My.sln", "-d")
                .ReadDirectoryBuildProps);
        Assert.Equal(
            true,
            Parse("-s", "My.sln", "-o")
                .ReportOverridenDirectoryBuildProps);
    }

    [Fact]
    public void A_toggle_passed_without_a_value_swallows_the_option_that_follows_it()
    {
        // The other side of being a scalar rather than a switch: with no value of its own, `-d` takes the next
        // token as one, and `-f` is not a bool. The bare form only survives at the end of the command line or in
        // front of a sequence option (`-s`, `-p`, `-e`), which is why the README spells out `-d false`.
        using var parser = CommandLineParserFactory.Create();

        Assert.IsType<NotParsed<Options>>(
            parser.ParseArguments<Options>(new[] { "-s", "My.sln", "-d", "-f", "json" }));
    }

    [Fact]
    public void Options_built_in_code_carry_the_same_defaults_as_the_command_line()
    {
        // The C# initializers on Options duplicate the Default values on the attributes; if the two drift apart,
        // an Options built in a test stops behaving like one built from a command line.
        var fromCommandLine = Parse("-s", "My.sln");
        var fromCode = new Options();

        Assert.Equal(fromCommandLine.Format, fromCode.Format);
        Assert.Equal(fromCommandLine.ReadDirectoryBuildProps, fromCode.ReadDirectoryBuildProps);
        Assert.Equal(fromCommandLine.ReportOverridenDirectoryBuildProps, fromCode.ReportOverridenDirectoryBuildProps);
    }

    private static Options Parse(params string[] args)
    {
        using var parser = CommandLineParserFactory.Create();

        return Assert.IsType<Parsed<Options>>(parser.ParseArguments<Options>(args))
            .Value;
    }
}
