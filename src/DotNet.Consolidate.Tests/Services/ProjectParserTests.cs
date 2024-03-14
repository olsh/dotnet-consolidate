using DotNet.Consolidate.Services;
using DotNet.Consolidate.Tests.Helpers;

using Xunit;

namespace DotNet.Consolidate.Tests.Services
{
    public class ProjectParserTests
    {
        [Fact]
        public void Package_reference_project_parsed_correctly()
        {
            var parser = new ProjectParser(new Logger());
            var projectFile = FileHelper.ReadResource("PackageReference.csproj");

            var nuGetPackages = parser.ParseProjectContent(projectFile);

            Assert.Equal(2, nuGetPackages.Count);
        }

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
    }
}
