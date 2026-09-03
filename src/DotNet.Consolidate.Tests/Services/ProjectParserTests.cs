using System.Collections.Generic;
using System.Linq;

using DotNet.Consolidate.Models;
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

        [Fact]
        public void Package_references_in_a_false_condition_item_group_are_skipped()
        {
            var packages = ParseConditionsProject();

            Assert.DoesNotContain(packages, p => p.Id == "Excluded.ByItemGroup");
        }

        [Fact]
        public void Package_reference_with_a_false_condition_is_skipped()
        {
            var packages = ParseConditionsProject();

            Assert.DoesNotContain(packages, p => p.Id == "Excluded.ByReference");
        }

        [Theory]
        [InlineData("Included.Always")]
        [InlineData("Included.WhenDebug")]
        [InlineData("Included.NotEqual")]
        [InlineData("Included.Grouping")]
        public void Package_reference_with_a_true_condition_is_included(string packageId)
        {
            var packages = ParseConditionsProject();

            Assert.Contains(packages, p => p.Id == packageId);
        }

        [Fact]
        public void Global_properties_activate_a_conditional_package_reference()
        {
            var packages = ParseConditionsProject(new Dictionary<string, string> { ["NuGetBuild"] = "true" });

            Assert.Contains(packages, p => p.Id == "Excluded.ByItemGroup");
            Assert.Contains(packages, p => p.Id == "Excluded.ByReference");
            Assert.DoesNotContain(packages, p => p.Id == "Included.NotEqual");
        }

        [Fact]
        public void Global_properties_are_not_overridden_by_the_project_file()
        {
            // The project sets SerilogVersion to 3.1.1; a global property has to win over that.
            var packages = ParseConditionsProject(new Dictionary<string, string> { ["SerilogVersion"] = "4.0.0" });

            var package = packages.Single(p => p.Id == "Included.Expanded");
            Assert.Equal("4.0.0", package.Version.OriginalValue);
        }

        [Fact]
        public void Property_reference_in_a_version_is_expanded()
        {
            var packages = ParseConditionsProject();

            var package = packages.Single(p => p.Id == "Included.Expanded");
            Assert.Equal("3.1.1", package.Version.OriginalValue);
        }

        [Fact]
        public void Unresolvable_version_property_keeps_the_original_value()
        {
            var packages = ParseConditionsProject();

            var package = packages.Single(p => p.Id == "Included.Unexpanded");
            Assert.Equal("$(UnknownVersion)", package.Version.OriginalValue);
        }

        [Fact]
        public void Unevaluatable_condition_keeps_the_package_reference()
        {
            var packages = ParseConditionsProject();

            Assert.Contains(packages, p => p.Id == "Included.Unevaluatable");
        }

        [Fact]
        public void Multi_targeting_project_unions_package_references_across_target_frameworks()
        {
            var parser = GetParser();
            var projectFile = FileHelper.ReadResource("MultiTargeting.csproj");

            var packages = parser.ParseProjectContent(projectFile);

            Assert.Equal(3, packages.Count);
            Assert.Contains(packages, p => p.Id == "System.Text.Json");
            Assert.Contains(packages, p => p.Id == "Microsoft.Extensions.Logging");
        }

        [Fact]
        public void Package_reference_shared_by_all_target_frameworks_is_returned_once()
        {
            var parser = GetParser();
            var projectFile = FileHelper.ReadResource("MultiTargeting.csproj");

            var packages = parser.ParseProjectContent(projectFile);

            Assert.Single(packages, p => p.Id == "Serilog");
        }

        private static List<NuGetPackageInfo> ParseConditionsProject(
            IReadOnlyDictionary<string, string> globalProperties = null)
        {
            var parser = new ProjectParser(new Logger(), globalProperties);
            var projectFile = FileHelper.ReadResource("Conditions.csproj");

            return parser.ParseProjectContent(projectFile);
        }

        private static ProjectParser GetParser()
        {
            return new ProjectParser(new Logger());
        }
    }
}
