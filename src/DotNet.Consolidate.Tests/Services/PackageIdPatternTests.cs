using DotNet.Consolidate.Services;

using Xunit;

namespace DotNet.Consolidate.Tests.Services;

public class PackageIdPatternTests
{
    [Theory]

    // The case from the issue: a family of packages selected by their shared prefix.
    [InlineData("MyCompany.*", "MyCompany.Dal", true)]
    [InlineData("MyCompany.*", "MyCompany.Logging", true)]
    [InlineData("MyCompany.*", "MyCompany.", true)]
    [InlineData("MyCompany.*", "MyCompany", false)]
    [InlineData("MyCompany.*", "Other.MyCompany.Dal", false)]

    // The dot is a literal, not "any character" -- this is a wildcard pattern, not a regular expression.
    [InlineData("MyCompany.*", "MyCompanyXDal", false)]
    [InlineData("Serilo.", "Serilog", false)]

    // Wildcards fold case the same way plain IDs do, since NuGet treats IDs case-insensitively.
    [InlineData("mycompany.*", "MyCompany.Dal", true)]
    [InlineData("MYCOMPANY.*", "mycompany.dal", true)]
    [InlineData("*.tests", "MyCompany.Tests", true)]
    [InlineData("*.Tests", "MyCompany.Tests", true)]
    [InlineData("*.Tests", "MyCompany.Tests.Core", false)]
    [InlineData("*Serilog*", "Serilog.Sinks.Console", true)]
    [InlineData("*Serilog*", "Moq", false)]
    [InlineData("MyCompany.*.Tests", "MyCompany.Dal.Tests", true)]
    [InlineData("MyCompany.*.Tests", "MyCompany.Tests", false)]

    // The match has to resume from the last `*` when the tail disagrees.
    [InlineData("A*B*C", "AxxBxxC", true)]
    [InlineData("A*B*C", "AxxBxx", false)]
    [InlineData("A*B*C", "AxxCxxB", false)]
    [InlineData("**Serilog**", "Serilog", true)]

    // `?` stands for exactly one character, never none.
    [InlineData("Serilog.?", "Serilog.A", true)]
    [InlineData("Serilog.?", "Serilog.AB", false)]
    [InlineData("Serilog.?", "Serilog.", false)]
    [InlineData("?erilog", "Serilog", true)]

    // An entry without a wildcard still has to name the package in full.
    [InlineData("Serilog", "Serilog", true)]
    [InlineData("Serilog", "serilog", true)]
    [InlineData("Serilog", "Serilog.Sinks.Console", false)]
    [InlineData("Serilog.Sinks", "Serilog", false)]
    [InlineData("*", "Serilog", true)]
    [InlineData("*", "", true)]
    [InlineData("", "", true)]
    [InlineData("", "Serilog", false)]
    public void A_package_id_pattern_matches_the_ids_it_names(string pattern, string packageId, bool expected)
    {
        var actual = PackageIdPattern.IsMatch(pattern, packageId);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Plain_ids_and_patterns_can_be_given_together()
    {
        var patterns = new[] { "Serilog", "MyCompany.*" };

        Assert.True(PackageIdPattern.IsMatchAny(patterns, "Serilog"));
        Assert.True(PackageIdPattern.IsMatchAny(patterns, "MyCompany.Dal"));
        Assert.False(PackageIdPattern.IsMatchAny(patterns, "Moq"));
    }

    [Fact]
    public void A_pattern_of_nothing_but_wildcards_still_answers()
    {
        // Translated to a regular expression this would be chained `.*`, which a backtracking engine takes
        // exponential time over. The scan the matcher uses is linear whatever the pattern looks like, so this
        // returns rather than hanging the run.
        var pattern = new string('*', 20) + "b";

        Assert.False(PackageIdPattern.IsMatch(pattern, new string('a', 200)));
        Assert.True(PackageIdPattern.IsMatch(pattern, new string('a', 200) + "b"));
    }
}
