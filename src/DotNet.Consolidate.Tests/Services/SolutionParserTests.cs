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
        private static string TestSolutionDirectoryName => Path.Join(new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName, "TestData", "TestSolution");

        private static string TestSolutionFileName => Path.Join(TestSolutionDirectoryName, "TestSolution.sln");

        [Fact]
        public void Solution_with_DirectoryBuildProps_parsed_correctly_when_allowed_to_read_them()
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), true);

            var solutions = new[] { TestSolutionFileName };

            // Act
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            // Assert
            Assert.NotNull(solution);
            Assert.Equal(2, solution.DirectoryBuildPropsInfos.Count);
        }

        [Fact]
        public void Solution_with_DirectoryBuildProps_parsed_correctly_when_not_allowed_to_read_them()
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), false);

            var solutions = new[] { TestSolutionFileName };

            // Act
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            // Assert
            Assert.NotNull(solution);
            Assert.Empty(solution.DirectoryBuildPropsInfos);
        }

        [Fact]
        public void Solution_with_DirectoryBuildProps_when_allowed_to_read_them_determines_project_references_correctly()
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), true);

            var solutions = new[] { TestSolutionFileName };

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
            Assert.Equal(2, projectA.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));

            Assert.NotNull(projectB);
            Assert.Equal(3, projectB.Packages.Count);
            Assert.Equal(1, projectB.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct));
            Assert.Equal(2, projectB.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));

            Assert.NotNull(projectATests);
            Assert.Equal(7, projectATests.Packages.Count);
            Assert.Equal(0, projectATests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct));
            Assert.Equal(7, projectATests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));

            Assert.NotNull(projectBTests);
            Assert.Equal(7, projectBTests.Packages.Count);
            Assert.Equal(0, projectBTests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct));
            Assert.Equal(7, projectBTests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));
        }

        [Fact]
        public void Solution_with_DirectoryBuildProps_when_not_allowed_to_read_them_determines_project_references_correctly()
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), false);

            var solutions = new[] { TestSolutionFileName };

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
            Assert.Equal(0, projectA.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));

            Assert.NotNull(projectB);
            Assert.Single(projectB.Packages);
            Assert.Equal(1, projectB.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct));
            Assert.Equal(0, projectB.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));

            Assert.NotNull(projectATests);
            Assert.Empty(projectATests.Packages);
            Assert.Equal(0, projectATests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct));
            Assert.Equal(0, projectATests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));

            Assert.NotNull(projectBTests);
            Assert.Empty(projectBTests.Packages);
            Assert.Equal(0, projectBTests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Direct));
            Assert.Equal(0, projectBTests.Packages.Count(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited));
        }
    }
}
