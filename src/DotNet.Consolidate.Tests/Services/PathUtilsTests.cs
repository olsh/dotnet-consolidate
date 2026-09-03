using System.Collections.Generic;
using System.IO;

using DotNet.Consolidate.Services;

using Xunit;

namespace DotNet.Consolidate.Tests.Services
{
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
            yield return new object[]
                { "/full/posix/path", Path.DirectorySeparatorChar + Path.Combine("full", "posix", "path") };
            yield return new object[]
                { "\\windows\\posix\\path", Path.DirectorySeparatorChar + Path.Combine("windows", "posix", "path") };
        }

        [Theory]
        [MemberData(nameof(PathTestData))]
        public void GivenPathWithAnySeparator_WhenEnsureSystemSeparator_AssertReturnedPathIsCorrect(
            string input,
            string expected)
        {
            var actual = PathUtils.EnsureSystemSeparator(input);

            Assert.Equal(expected, actual);
        }

        public static IEnumerable<object[]> DirectoryContainmentTestData()
        {
            var project = Path.Combine("src", "Project");

            yield return new object[] { project, project, true };
            yield return new object[] { Path.Combine("src", "Project", "Sub"), project, true };
            yield return new object[] { Path.Combine("src", "Project", "a", "b", "c"), project, true };

            // The bug: `src\ProjectB` starts with `src\Project`, but the match doesn't end on a boundary.
            yield return new object[] { Path.Combine("src", "ProjectB"), project, false };
            yield return new object[] { Path.Combine("src", "ProjectB", "Sub"), project, false };
            yield return new object[] { Path.Combine("src", "Project.Tests"), project, false };

            yield return new object[] { Path.Combine("SRC", "pROJECT", "Sub"), project, true };

            yield return new object[]
            {
                Path.Combine("src", "Project", "Sub"), project + Path.DirectorySeparatorChar, true,
            };
            yield return new object[] { project + Path.DirectorySeparatorChar, project, true };

            yield return new object[] { Path.Combine("tests", "Project"), project, false };
            yield return new object[] { "src", project, false };
        }

        [Theory]
        [MemberData(nameof(DirectoryContainmentTestData))]
        public void Directory_containment_stops_at_a_directory_boundary(
            string directory,
            string ancestorDirectory,
            bool expected)
        {
            var actual = PathUtils.IsSameOrUnderDirectory(directory, ancestorDirectory);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void The_file_system_root_contains_every_path_below_it()
        {
            // A root keeps its trailing separator, which is the one case where the boundary is already
            // part of the matched prefix.
            var root = Path.GetPathRoot(Path.GetFullPath("."));
            Assert.True(Path.EndsInDirectorySeparator(root));

            Assert.True(PathUtils.IsSameOrUnderDirectory(root, root));
            Assert.True(PathUtils.IsSameOrUnderDirectory(Path.Combine(root, "src", "Project"), root));
        }
    }
}
