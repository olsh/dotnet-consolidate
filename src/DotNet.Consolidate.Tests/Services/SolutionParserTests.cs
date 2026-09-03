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

            // Pinned as an exact id-and-version set rather than a count and a couple of ids: the versions
            // are what the defect got wrong, and a project handed the wrong props file still ends up with a
            // plausible-looking package list. Counting ids only holds while the two props files declare
            // disjoint packages, which is not something this test should depend on.
            var inheritedPackages = integrationTests.Packages
                .Where(p => p.PackageReferenceType == NuGetPackageReferenceType.Inherited)
                .Select(p => $"{p.Id} {p.Version.OriginalValue}")
                .OrderBy(p => p)
                .ToList();

            Assert.Equal(new[] { "CommandLineParser 2.7.82", "Serilog 3.0.1" }, inheritedPackages);
        }

        [Theory]
        [InlineData("TestSolution.sln")]
        [InlineData("TestSolution.slnx")]
        public void Each_project_records_the_DirectoryBuildProps_file_it_inherits_from(string solutionFileName)
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), true);

            var solutions = new[] { TestSolutionFileName(solutionFileName) };

            // Act
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            // Assert
            Assert.NotNull(solution);

            // The nearest ancestor, which is the same file the inherited packages came from — reporting an
            // override is no use without naming the props file to go and change.
            var projectATests = solution.ProjectInfos.First(p => p.ProjectName.Equals("ProjectA.Tests"));
            Assert.Equal(
                Path.Join(TestSolutionDirectoryName, "tests", "Directory.build.props"),
                projectATests.DirectoryBuildPropsFile);

            // tests-integration\ is a sibling of tests\, so this one inherits from the solution root instead.
            var integrationTests = solution.ProjectInfos.First(p => p.ProjectName.Equals("IntegrationTests"));
            Assert.Equal(
                Path.Join(TestSolutionDirectoryName, "Directory.build.props"),
                integrationTests.DirectoryBuildPropsFile);
        }

        [Theory]
        [InlineData("TestSolution.sln")]
        [InlineData("TestSolution.slnx")]
        public void No_DirectoryBuildProps_file_is_recorded_when_they_are_not_read(string solutionFileName)
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), false);

            var solutions = new[] { TestSolutionFileName(solutionFileName) };

            // Act
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            // Assert
            Assert.NotNull(solution);
            Assert.All(solution.ProjectInfos, p => Assert.Null(p.DirectoryBuildPropsFile));
        }

        [Theory]
        [InlineData("TestSolution.sln")]
        [InlineData("TestSolution.slnx")]
        public void A_project_that_updates_an_inherited_package_is_analyzed_at_the_updated_version(
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

            // The props file and the csproj are parsed separately, so this is the end-to-end proof that the
            // update finds the inherited item after the two have been brought together.
            var projectC = solution.ProjectInfos.First(p => p.ProjectName.Equals("ProjectC"));
            var packages = PackagesAnalyzer.GetEffectivePackages(projectC)
                .Select(p => $"{p.Id} {p.Version.OriginalValue}")
                .OrderBy(p => p)
                .ToList();

            // Serilog once, at the version the csproj set — not twice, and not at the props file's 3.0.1.
            Assert.Equal(new[] { "CommandLineParser 2.7.82", "Serilog 4.0.0" }, packages);
        }

        [Theory]
        [InlineData("TestSolution.sln")]
        [InlineData("TestSolution.slnx")]
        public void A_project_that_removes_an_inherited_package_no_longer_references_it(string solutionFileName)
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), true);

            var solutions = new[] { TestSolutionFileName(solutionFileName) };

            // Act
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            // Assert
            Assert.NotNull(solution);

            var projectD = solution.ProjectInfos.First(p => p.ProjectName.Equals("ProjectD"));
            var packages = PackagesAnalyzer.GetEffectivePackages(projectD)
                .Select(p => $"{p.Id} {p.Version.OriginalValue}")
                .ToList();

            Assert.Equal(new[] { "Serilog 3.0.1" }, packages);
        }

        [Theory]
        [InlineData("TestSolution.sln")]
        [InlineData("TestSolution.slnx")]
        public void An_update_contributes_nothing_when_DirectoryBuildProps_are_not_read(string solutionFileName)
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), false);

            var solutions = new[] { TestSolutionFileName(solutionFileName) };

            // Act
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            // Assert
            Assert.NotNull(solution);

            // With nothing inherited there is no item for the update to name, and an update is not a
            // reference in its own right.
            var projectC = solution.ProjectInfos.First(p => p.ProjectName.Equals("ProjectC"));
            Assert.Empty(PackagesAnalyzer.GetEffectivePackages(projectC));
        }

        [Theory]
        [InlineData("TestSolution.sln")]
        [InlineData("TestSolution.slnx")]
        public void A_project_that_updates_an_inherited_package_is_reported_as_an_override(string solutionFileName)
        {
            var projectParser = new ProjectParser(new Logger());
            var solutionInfoProvider = new SolutionInfoProvider(projectParser, new Logger(), true);

            var solutions = new[] { TestSolutionFileName(solutionFileName) };

            // Act
            var solution = solutionInfoProvider.GetSolutionsInfo(solutions)
                .FirstOrDefault();

            // Assert
            Assert.NotNull(solution);

            var overrides = PackagesAnalyzer.FindDirectoryBuildPropsOverrides(
                solution.ProjectInfos,
                new Options());

            var propsOverride = Assert.Single(overrides);
            Assert.Equal("ProjectC", propsOverride.ProjectName);
            Assert.Equal("Serilog", propsOverride.PackageId);
            Assert.Equal("4.0.0", propsOverride.ProjectVersion.OriginalValue);
            Assert.Equal("3.0.1", propsOverride.DirectoryBuildPropsVersion.OriginalValue);
            Assert.Equal(
                Path.Join(TestSolutionDirectoryName, "Directory.build.props"),
                propsOverride.DirectoryBuildPropsFile);
        }

        private static string TestSolutionFileName(string solutionFileName) =>
            Path.Join(TestSolutionDirectoryName, solutionFileName);
    }
}
