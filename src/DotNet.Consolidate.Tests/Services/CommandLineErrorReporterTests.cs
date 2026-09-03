using System.IO;
using System.Linq;
using System.Text.Json;

using CommandLine;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;

using Xunit;

namespace DotNet.Consolidate.Tests.Services;

/// <summary>
/// Guards what a command line the parser rejects does: it has to fail the run, and in the JSON format it still owes
/// stdout a document. A parse error is handled before anything else in the tool runs, so nothing else covers it.
/// </summary>
public class CommandLineErrorReporterTests
{
    [Fact]
    public void An_unknown_option_fails_the_run()
    {
        var (isFailure, output) = Report("--definitely-not-an-option");

        Assert.True(isFailure);

        // The parser's HelpWriter has already written the message to stderr; printing it again on stdout would
        // only be worse, and would break `-f json`.
        Assert.Equal(string.Empty, output);
    }

    [Fact]
    public void An_option_repeated_instead_of_given_a_list_fails_the_run()
    {
        // `-p` takes a space separated list, so repeating it is easy to write by accident.
        var (isFailure, output) = Report("-s", "My.sln", "-p", "Serilog", "-p", "Newtonsoft.Json");

        Assert.True(isFailure);
        Assert.Equal(string.Empty, output);
    }

    [Fact]
    public void Help_is_not_a_failure()
    {
        var (isFailure, output) = Report("--help");

        Assert.False(isFailure);
        Assert.Equal(string.Empty, output);
    }

    [Fact]
    public void Version_is_not_a_failure()
    {
        var (isFailure, output) = Report("--version");

        Assert.False(isFailure);
        Assert.Equal(string.Empty, output);
    }

    [Fact]
    public void An_unknown_option_is_named_in_the_json_document()
    {
        var (isFailure, output) = Report("--definitely-not-an-option", "-f", "json");

        Assert.True(isFailure);

        using var document = JsonDocument.Parse(output);
        var warning = Assert.Single(
            document.RootElement.GetProperty("warnings")
                .EnumerateArray());

        // The error's own sentence, not its tag: `UnknownOptionError` wouldn't say which option was wrong.
        Assert.Contains("definitely-not-an-option", warning.GetString());
        Assert.Empty(
            document.RootElement.GetProperty("solutions")
                .EnumerateArray());
    }

    [Fact]
    public void A_repeated_option_is_named_in_the_json_document()
    {
        var (_, output) = Report("-s", "My.sln", "-p", "Serilog", "-p", "Newtonsoft.Json", "-f", "json");

        using var document = JsonDocument.Parse(output);
        var warning = Assert.Single(
            document.RootElement.GetProperty("warnings")
                .EnumerateArray());

        Assert.Contains("packageIds", warning.GetString());
    }

    [Theory]
    [InlineData("-f", "json")]
    [InlineData("-f", "JSON")]
    [InlineData("--format", "json")]
    public void The_format_is_recognized_however_it_was_written(string name, string value)
    {
        // The format has to be recovered from the raw arguments, so the spellings the real parser accepts have to
        // keep working after it has rejected the rest of the command line.
        var (_, output) = Report("--definitely-not-an-option", name, value);

        using var document = JsonDocument.Parse(output);
        Assert.Single(
            document.RootElement.GetProperty("warnings")
                .EnumerateArray());
    }

    [Fact]
    public void The_format_is_recognized_when_it_is_assigned_with_an_equals_sign()
    {
        var (_, output) = Report("--definitely-not-an-option", "--format=json");

        using var document = JsonDocument.Parse(output);
        Assert.Single(
            document.RootElement.GetProperty("warnings")
                .EnumerateArray());
    }

    [Fact]
    public void Every_error_is_reported()
    {
        var (_, output) = Report("--definitely-not-an-option", "--nor-this-one", "-f", "json");

        using var document = JsonDocument.Parse(output);
        Assert.Equal(
            2,
            document.RootElement.GetProperty("warnings")
                .EnumerateArray()
                .Count());
    }

    [Fact]
    public void An_unusable_format_leaves_stdout_alone()
    {
        // Nothing can be promised to a caller who asked for a format that doesn't exist, and half a document
        // would be worse than none.
        var (isFailure, output) = Report("--definitely-not-an-option", "-f", "xml");

        Assert.True(isFailure);
        Assert.Equal(string.Empty, output);
    }

    private static (bool IsFailure, string Output) Report(params string[] args)
    {
        // The errors have to come from a real parse: their constructors are internal, so a test can't build one.
        using var parser = CommandLineParserFactory.Create();
        var output = new StringWriter();
        var isFailure = false;

        parser.ParseArguments<Options>(args)
            .WithNotParsed(errors => isFailure = CommandLineErrorReporter.Report(args, errors, output));

        return (isFailure, output.ToString());
    }
}
