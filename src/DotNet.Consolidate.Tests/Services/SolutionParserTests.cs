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
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            Assert.NotNull(solution);
            Assert.Equal(2, solution.DirectoryBuildPropsInfos.Count);
        }

        [Fact]
        public void Solution_with_DirectoryBuildProps_parsed_correctly_when_not_allowed_to_read_them()
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), false);

            var solutions = new[] { TestSolutionFileName };
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            Assert.NotNull(solution);
            Assert.Equal(0, solution.DirectoryBuildPropsInfos.Count);
        }

        [Fact]
        public void Solution_with_DirectoryBuildProps_when_allowed_to_read_them_determines_project_references_correctly()
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), true);

            var solutions = new[] { TestSolutionFileName };
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            var projectA = solution.ProjectInfos.FirstOrDefault(p => p.ProjectName.Equals("ProjectA"));
            var projectB = solution.ProjectInfos.FirstOrDefault(p => p.ProjectName.Equals("ProjectB"));
            var projectATests = solution.ProjectInfos.FirstOrDefault(p => p.ProjectName.Equals("ProjectA.Tests"));
            var projectBTests = solution.ProjectInfos.FirstOrDefault(p => p.ProjectName.Equals("ProjectB.Tests"));

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

#if SHIT
        [Fact]
        public void Net_core_reference_project_parsed_correctly()
        {
            var parser = new ProjectParser(new Logger());
            var projectFile = FileHelper.ReadResource("NetCore.csproj");

            var nuGetPackages = parser.ParseProjectContent(projectFile);

            Assert.Equal(3, nuGetPackages.Count);
        }

        [Fact]
        public void Packages_config_parsed_correctly()
        {
            var parser = GetParser();
            var packagesConfig = FileHelper.ReadResource("packages.config");

            var nuGetPackages = parser.ParsePackageConfigContent(packagesConfig);

            Assert.Equal(2, nuGetPackages.Count);
        }

        [Fact]
        public void Directory_Build_props_reference_project_parsed_correctly()
        {
            var parser = new ProjectParser(new Logger());
            var projectFile = FileHelper.ReadResource("Directory.build.props");

            var nuGetPackages = parser.ParseProjectContent(projectFile);

            Assert.Equal(7, nuGetPackages.Count);
        }

        private static ProjectParser GetParser()
        {
            return new ProjectParser(new Logger());
        }
#endif
    }
}
