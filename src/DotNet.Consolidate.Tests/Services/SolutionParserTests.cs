using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using DotNet.Consolidate.Models;
using DotNet.Consolidate.Services;

using Xunit;

using ProjectParser = DotNet.Consolidate.Services.ProjectParser;

namespace DotNet.Consolidate.Tests.Services
{
    public class SolutionParserTests
    {
        private static string TestSolutionDirectoryName => Path.Join(
            new FileInfo(
                Assembly.GetExecutingAssembly()
                    .Location).DirectoryName,
            "TestData",
            "TestSolution");

        [Theory]
        [InlineData("TestSolution.sln")]
        [InlineData("TestSolution.slnx")]
        public void Solution_with_DirectoryBuildProps_parsed_correctly_when_allowed_to_read_them(
            string solutionFileName)
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), true);

            var solutions = new[] { TestSolutionFileName(solutionFileName) };

            // Act
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            // Assert
            Assert.NotNull(solution);
            Assert.True(solution.IsParsedWithoutIssues);
            Assert.Equal(2, solution.DirectoryBuildPropsInfos.Count);
        }

        [Theory]
        [InlineData("TestSolution.sln")]
        [InlineData("TestSolution.slnx")]
        public void Solution_with_DirectoryBuildProps_parsed_correctly_when_not_allowed_to_read_them(
            string solutionFileName)
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), false);

            var solutions = new[] { TestSolutionFileName(solutionFileName) };

            // Act
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            // Assert
            Assert.NotNull(solution);
            Assert.Empty(solution.DirectoryBuildPropsInfos);
        }

        [Theory]
        [InlineData("TestSolution.sln")]
        [InlineData("TestSolution.slnx")]
        public void Solution_with_DirectoryBuildProps_when_allowed_to_read_them_determines_project_references_correctly(
            string solutionFileName)
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), true);

            var solutions = new[] { TestSolutionFileName(solutionFileName) };

            // Act
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            var projectA = solution.ProjectInfos.FirstOrDefault(p => p.ProjectName.Equals("ProjectA"));
            var projectB = solution.ProjectInfos.FirstOrDefault(p => p.ProjectName.Equals("ProjectB"));
            var projectATests = solution.ProjectInfos.FirstOrDefault(p => p.ProjectName.Equals("ProjectA.Tests"));
            var projectBTests = solution.ProjectInfos.FirstOrDefault(p => p.ProjectName.Equals("ProjectB.Tests"));

            // Assert
            Assert.NotNull(projectA);
            Assert.Equal(2, projectA.Packages.Count);
            Assert.Equal(0, projectA.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct));
            Assert.Equal(
                2,
                projectA.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));

            Assert.NotNull(projectB);
            Assert.Equal(3, projectB.Packages.Count);
            Assert.Equal(1, projectB.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct));
            Assert.Equal(
                2,
                projectB.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));

            Assert.NotNull(projectATests);
            Assert.Equal(7, projectATests.Packages.Count);
            Assert.Equal(
                0,
                projectATests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct));
            Assert.Equal(
                7,
                projectATests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));

            Assert.NotNull(projectBTests);
            Assert.Equal(7, projectBTests.Packages.Count);
            Assert.Equal(
                0,
                projectBTests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct));
            Assert.Equal(
                7,
                projectBTests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));
        }

        [Theory]
        [InlineData("TestSolution.sln")]
        [InlineData("TestSolution.slnx")]
        public void
            Solution_with_DirectoryBuildProps_when_not_allowed_to_read_them_determines_project_references_correctly(
                string solutionFileName)
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), false);

            var solutions = new[] { TestSolutionFileName(solutionFileName) };

            // Act
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            var projectA = solution.ProjectInfos.FirstOrDefault(p => p.ProjectName.Equals("ProjectA"));
            var projectB = solution.ProjectInfos.FirstOrDefault(p => p.ProjectName.Equals("ProjectB"));
            var projectATests = solution.ProjectInfos.FirstOrDefault(p => p.ProjectName.Equals("ProjectA.Tests"));
            var projectBTests = solution.ProjectInfos.FirstOrDefault(p => p.ProjectName.Equals("ProjectB.Tests"));

            // Assert
            Assert.NotNull(projectA);
            Assert.Empty(projectA.Packages);
            Assert.Equal(0, projectA.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct));
            Assert.Equal(
                0,
                projectA.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));

            Assert.NotNull(projectB);
            Assert.Single(projectB.Packages);
            Assert.Equal(1, projectB.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct));
            Assert.Equal(
                0,
                projectB.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));

            Assert.NotNull(projectATests);
            Assert.Empty(projectATests.Packages);
            Assert.Equal(
                0,
                projectATests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct));
            Assert.Equal(
                0,
                projectATests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));

            Assert.NotNull(projectBTests);
            Assert.Empty(projectBTests.Packages);
            Assert.Equal(
                0,
                projectBTests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct));
            Assert.Equal(
                0,
                projectBTests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));
        }

        [Theory]
        [InlineData("TestSolution.sln")]
        [InlineData("TestSolution.slnx")]
        public void Solution_conditional_package_reference_is_included_when_the_property_is_supplied(
            string solutionFileName)
        {
            var globalProperties = new Dictionary<string, string> { ["NuGetBuild"] = "true" };
            var projectParser = new ProjectParser(new Logger(), globalProperties);
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), false);

            var solutions = new[] { TestSolutionFileName(solutionFileName) };

            // Act
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            var projectA = solution.ProjectInfos.FirstOrDefault(p => p.ProjectName.Equals("ProjectA"));

            // Assert
            Assert.NotNull(projectA);
            Assert.Single(projectA.Packages);

            var package = projectA.Packages.Single();
            Assert.Equal("Newtonsoft.Json", package.Id);
        }

        [Theory]
        [InlineData("TestSolution.sln")]
        [InlineData("TestSolution.slnx")]
        public void Solution_given_by_a_relative_path_still_inherits_DirectoryBuildProps_packages(
            string solutionFileName)
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), true);

            // Directory.Build.props directories are always absolute, while project directories follow the
            // solution path as given, so matching them used to find nothing at all for a relative solution.
            var relativeSolutionPath = Path.GetRelativePath(
                Directory.GetCurrentDirectory(),
                TestSolutionFileName(solutionFileName));
            Assert.False(Path.IsPathRooted(relativeSolutionPath));

            // Act
            var solution = solutionInfoProvider.GetSolutionsInfo(new[] { relativeSolutionPath })
                .FirstOrDefault();

            // Assert
            Assert.NotNull(solution);

            var projectB = solution.ProjectInfos.FirstOrDefault(p => p.ProjectName.Equals("ProjectB"));
            Assert.NotNull(projectB);
            Assert.Equal(
                2,
                projectB.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));
            Assert.Contains(projectB.Packages, p => p.Id == "Serilog");
        }

        [Theory]
        [InlineData("TestSolution.sln")]
        [InlineData("TestSolution.slnx")]
        public void Solution_project_does_not_inherit_from_a_sibling_directory_sharing_a_name_prefix(
            string solutionFileName)
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), true);

            var solutions = new[] { TestSolutionFileName(solutionFileName) };

            // Act
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            // Assert
            // tests-integration\ is a sibling of tests\, not a child of it, so IntegrationTests inherits from
            // the solution root. A bare string prefix match claimed it for tests\Directory.build.props, which
            // also wins the longest-first ordering, and the project silently got the wrong versions.
            var integrationTests = solution.ProjectInfos
                .FirstOrDefault(p => p.ProjectName.Equals("IntegrationTests"));

            Assert.NotNull(integrationTests);
            Assert.Equal(
                2,
                integrationTests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));
            Assert.Contains(integrationTests.Packages, p => p.Id == "Serilog");
            Assert.DoesNotContain(integrationTests.Packages, p => p.Id == "NUnit");
        }

        private static string TestSolutionFileName(string solutionFileName) =>
            Path.Join(TestSolutionDirectoryName, solutionFileName);
    }
}
