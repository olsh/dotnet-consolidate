using System.Collections.Generic;
using System.IO;
using DotNet.Consolidate.Services;
using Xunit;

namespace DotNet.Consolidate.Tests.Services;

public class PathUtilsTests
{
    public static IEnumerable<object[]> PathTestData()
    {
        yield return new object[] { "a/posix/path", Path.Combine("a", "posix", "path") };
        yield return new object[] { "a\\windows\\path", Path.Combine("a", "windows", "path") };
        yield return new object[] { "a\\mixed/path", Path.Combine("a", "mixed", "path") };
        yield return new object[] { "single", Path.Combine("single") };
        yield return new object[] { "C:\\full\\windows\\path", Path.Combine("C:", "full", "windows", "path") };
        yield return new object[] { string.Empty, string.Empty };
        yield return new object[] { ".\\explicit\\relative", Path.Combine(".", "explicit", "relative") };
        yield return new object[] { "/full/posix/path", Path.DirectorySeparatorChar + Path.Combine("full", "posix", "path") };
        yield return new object[] { "\\windows\\posix\\path", Path.DirectorySeparatorChar + Path.Combine("windows", "posix", "path") };
    }

    [Theory]
    [MemberData(nameof(PathTestData))]
    public void GivenPathWithAnySeparator_WhenEnsureSystemSeparator_AssertReturnedPathIsCorrect(string input, string expected)
    {
        var actual = PathUtils.EnsureSystemSeparator(input);

        Assert.Equal(expected, actual);
    }
}