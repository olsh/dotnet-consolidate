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

            var nuGetPackages = parser.ParseProjectContent(projectFile)
                .Packages;

            Assert.Equal(2, nuGetPackages.Count);
        }

        [Fact]
        public void Net_core_reference_project_parsed_correctly()
        {
            var parser = new ProjectParser(new Logger());
            var projectFile = FileHelper.ReadResource("NetCore.csproj");

            var nuGetPackages = parser.ParseProjectContent(projectFile)
                .Packages;

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

            var nuGetPackages = parser.ParseProjectContent(projectFile)
                .Packages;

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

            var packages = parser.ParseProjectContent(projectFile)
                .Packages;

            Assert.Equal(3, packages.Count);
            Assert.Contains(packages, p => p.Id == "System.Text.Json");
            Assert.Contains(packages, p => p.Id == "Microsoft.Extensions.Logging");
        }

        [Fact]
        public void Package_reference_shared_by_all_target_frameworks_is_returned_once()
        {
            var parser = GetParser();
            var projectFile = FileHelper.ReadResource("MultiTargeting.csproj");

            var packages = parser.ParseProjectContent(projectFile)
                .Packages;

            Assert.Single(packages, p => p.Id == "Serilog");
        }

        [Fact]
        public void A_package_reference_update_is_not_a_package_reference()
        {
            // The whole point of keeping updates out of the package list: on its own an update adds nothing,
            // so a project that only ever updates an inherited package must look like it references nothing.
            var result = ParseUpdatesProject();

            Assert.DoesNotContain(result.Packages, p => p.Id == "Inherited.Only");
            Assert.Contains(result.PackageUpdates, u => u.Id == "Inherited.Only" && u.Version.OriginalValue == "4.0.0");
        }

        [Fact]
        public void An_update_below_its_own_include_changes_the_declared_version()
        {
            var result = ParseUpdatesProject();

            var package = result.Packages.Single(p => p.Id == "Updated.After");
            Assert.Equal("2.0.0", package.Version.OriginalValue);
        }

        [Fact]
        public void An_update_above_its_own_include_leaves_the_declared_version_alone()
        {
            // MSBuild applies an update to the items declared before it, and this one has none to apply to.
            var result = ParseUpdatesProject();

            var package = result.Packages.Single(p => p.Id == "Updated.Before");
            Assert.Equal("1.0.0", package.Version.OriginalValue);

            // It is still carried out of the file, because the inherited items all precede the csproj body.
            Assert.Contains(result.PackageUpdates, u => u.Id == "Updated.Before");
        }

        [Fact]
        public void A_removal_below_its_own_include_drops_the_package()
        {
            var result = ParseUpdatesProject();

            Assert.DoesNotContain(result.Packages, p => p.Id == "Removed.After");
            Assert.Contains(result.RemovedPackageIds, id => id == "Removed.After");
        }

        [Fact]
        public void A_removal_above_its_own_include_keeps_the_declared_package()
        {
            // This is the NU1504-free way to override an inherited version: drop it, then declare your own.
            var result = ParseUpdatesProject();

            Assert.Contains(result.Packages, p => p.Id == "Removed.Before");
            Assert.Contains(result.RemovedPackageIds, id => id == "Removed.Before");
        }

        [Fact]
        public void The_last_update_of_a_package_wins()
        {
            var result = ParseUpdatesProject();

            var update = result.PackageUpdates.Single(u => u.Id == "Updated.Twice");
            Assert.Equal("3.0.0", update.Version.OriginalValue);
        }

        [Fact]
        public void An_update_without_a_version_is_not_a_version_change()
        {
            // `<PackageReference Update="X" PrivateAssets="all" />` is metadata, and very common.
            var result = ParseUpdatesProject();

            Assert.DoesNotContain(result.PackageUpdates, u => u.Id == "Metadata.Only");
            Assert.DoesNotContain(result.Packages, p => p.Id == "Metadata.Only");
        }

        [Fact]
        public void Property_reference_in_an_update_version_is_expanded()
        {
            var result = ParseUpdatesProject();

            var update = result.PackageUpdates.Single(u => u.Id == "Updated.Expanded");
            Assert.Equal("3.1.1", update.Version.OriginalValue);
        }

        [Fact]
        public void Unresolvable_version_property_drops_the_update()
        {
            // Unlike an include, which keeps the literal text: there the alternative is losing the reference,
            // while here it would overwrite a real inherited version with "$(UnknownVersion)".
            var result = ParseUpdatesProject();

            Assert.DoesNotContain(result.PackageUpdates, u => u.Id == "Updated.Unexpanded");
        }

        [Theory]
        [InlineData("Updated.First")]
        [InlineData("Updated.Second")]
        public void A_semicolon_separated_update_names_every_package_in_it(string packageId)
        {
            var result = ParseUpdatesProject();

            Assert.Contains(result.PackageUpdates, u => u.Id == packageId && u.Version.OriginalValue == "5.0.0");
        }

        [Theory]
        [InlineData("Removed.First")]
        [InlineData("Removed.Second")]
        public void A_semicolon_separated_removal_names_every_package_in_it(string packageId)
        {
            var result = ParseUpdatesProject();

            Assert.Contains(result.RemovedPackageIds, id => id == packageId);
        }

        [Fact]
        public void An_update_or_removal_with_a_false_condition_is_skipped()
        {
            var result = ParseUpdatesProject();

            Assert.DoesNotContain(result.PackageUpdates, u => u.Id == "Excluded.Update");
            Assert.DoesNotContain(result.RemovedPackageIds, id => id == "Excluded.Remove");
            Assert.DoesNotContain(result.PackageUpdates, u => u.Id == "Excluded.ByItemGroup");
            Assert.DoesNotContain(result.RemovedPackageIds, id => id == "Removed.ByItemGroup");
        }

        [Fact]
        public void An_unevaluatable_condition_discards_a_removal()
        {
            // A condition the tool failed to parse must never be the reason a package disappears. An include
            // behind one is kept for the same reason; a removal is the one form that can subtract.
            var result = ParseUpdatesProject();

            Assert.DoesNotContain(result.RemovedPackageIds, id => id == "Unevaluatable.Remove");
        }

        [Fact]
        public void An_unevaluatable_condition_keeps_an_update_but_not_as_a_replacement()
        {
            var result = ParseUpdatesProject();

            var update = result.PackageUpdates.Single(u => u.Id == "Unevaluatable.Update");
            Assert.False(update.ReplacesInheritedVersion);
        }

        [Fact]
        public void An_update_made_by_every_target_framework_replaces_the_inherited_version()
        {
            var result = ParseUpdatesProject("MultiTargetingUpdates.csproj");

            var update = result.PackageUpdates.Single(u => u.Id == "Fully.Updated");
            Assert.True(update.ReplacesInheritedVersion);
        }

        [Fact]
        public void An_update_made_by_one_target_framework_does_not_replace_the_inherited_version()
        {
            // net472 restores 2.0.0 and net8.0 the inherited version, so both are really referenced.
            var result = ParseUpdatesProject("MultiTargetingUpdates.csproj");

            var update = result.PackageUpdates.Single(u => u.Id == "Partially.Updated");
            Assert.False(update.ReplacesInheritedVersion);
        }

        [Fact]
        public void A_package_is_removed_only_when_every_target_framework_removes_it()
        {
            var result = ParseUpdatesProject("MultiTargetingUpdates.csproj");

            Assert.Contains(result.RemovedPackageIds, id => id == "Fully.Removed");
            Assert.DoesNotContain(result.RemovedPackageIds, id => id == "Partially.Removed");
        }

        private static List<NuGetPackageInfo> ParseConditionsProject(
            IReadOnlyDictionary<string, string> globalProperties = null)
        {
            var parser = new ProjectParser(new Logger(), globalProperties);
            var projectFile = FileHelper.ReadResource("Conditions.csproj");

            return parser.ParseProjectContent(projectFile)
                .Packages;
        }

        private static ProjectEvaluationResult ParseUpdatesProject(string resourceName = "PackageUpdates.csproj")
        {
            var parser = GetParser();
            var projectFile = FileHelper.ReadResource(resourceName);

            return parser.ParseProjectContent(projectFile);
        }

        private static ProjectParser GetParser()
        {
            return new ProjectParser(new Logger());
        }
    }
}
