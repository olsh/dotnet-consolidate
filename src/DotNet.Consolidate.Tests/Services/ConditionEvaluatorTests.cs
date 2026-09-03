using System.Collections.Generic;
using System.IO;
using System.Reflection;

using DotNet.Consolidate.Services;

using Xunit;

namespace DotNet.Consolidate.Tests.Services
{
    public class ConditionEvaluatorTests
    {
        private static string TestDirectoryName => new FileInfo(
            Assembly.GetExecutingAssembly()
                .Location).DirectoryName;

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Empty_condition_is_true(string condition)
        {
            Assert.True(Evaluate(condition));
        }

        [Theory]
        [InlineData("'$(Configuration)' == 'Debug'", true)]
        [InlineData("'$(Configuration)' == 'debug'", true)]
        [InlineData("'$(Configuration)' == 'Release'", false)]
        [InlineData("'$(Configuration)' != 'Release'", true)]
        public void String_comparison_is_case_insensitive(string condition, bool expected)
        {
            Assert.Equal(expected, Evaluate(condition));
        }

        [Theory]
        [InlineData("'$(NuGetBuild)' == 'true'", false)]
        [InlineData("'$(NuGetBuild)' != 'true'", true)]
        [InlineData("'$(NuGetBuild)' == ''", true)]
        public void Undefined_property_expands_to_an_empty_string(string condition, bool expected)
        {
            Assert.Equal(expected, Evaluate(condition));
        }

        [Theory]
        [InlineData("2 > 1", true)]
        [InlineData("1 >= 1", true)]
        [InlineData("1 < 0", false)]
        [InlineData("'2' <= '10'", true)]
        [InlineData("'1.0' == '1.00'", true)]
        [InlineData("0x10 == 16", true)]
        public void Numeric_comparison_operators_are_evaluated(string condition, bool expected)
        {
            Assert.Equal(expected, Evaluate(condition));
        }

        [Theory]
        [InlineData("'a' == 'a' And 'b' == 'b'", true)]
        [InlineData("'a' == 'a' AND 'b' == 'c'", false)]
        [InlineData("'a' == 'b' Or 'b' == 'b'", true)]
        [InlineData("'a' == 'b' or 'b' == 'c'", false)]
        [InlineData("!('a' == 'b')", true)]
        [InlineData("!true", false)]
        public void And_or_and_negation_are_evaluated(string condition, bool expected)
        {
            Assert.Equal(expected, Evaluate(condition));
        }

        [Theory]
        [InlineData("('a' == 'b' Or 'c' == 'c') And 'd' == 'd'", true)]
        [InlineData("'a' == 'b' Or ('c' == 'c' And 'd' == 'e')", false)]
        public void Parentheses_change_operator_precedence(string condition, bool expected)
        {
            Assert.Equal(expected, Evaluate(condition));
        }

        [Theory]
        [InlineData("'$(Configuration)|$(Platform)' == 'Debug|AnyCPU'", true)]
        [InlineData("'$(Configuration)|$(Platform)' == 'Release|AnyCPU'", false)]
        public void Combined_configuration_and_platform_condition_is_evaluated(string condition, bool expected)
        {
            Assert.Equal(expected, Evaluate(condition));
        }

        [Fact]
        public void Global_properties_take_precedence_over_the_seeded_defaults()
        {
            var globalProperties = new Dictionary<string, string> { ["Configuration"] = "Release" };

            Assert.True(Evaluate("'$(Configuration)' == 'Release'", globalProperties));
        }

        [Fact]
        public void Exists_resolves_relative_to_the_project_directory()
        {
            var projectFilePath = Path.Join(
                TestDirectoryName,
                "TestData",
                "TestSolution",
                "src",
                "ProjectA",
                "ProjectA.csproj");
            var properties = new MSBuildProperties(null, projectFilePath);
            Assert.True(ConditionEvaluator.TryEvaluate("Exists('ProjectA.csproj')", properties, out var exists));
            Assert.True(exists);

            Assert.True(ConditionEvaluator.TryEvaluate("Exists('NotThere.csproj')", properties, out var missing));
            Assert.False(missing);
        }

        [Theory]
        [InlineData("$([MSBuild]::VersionGreaterThan('$(TargetFramework)', 'net6.0'))")]
        [InlineData("@(Compile) != ''")]
        [InlineData("'$(Configuration)' = 'Debug'")]
        [InlineData("'$(Configuration)' == ")]
        [InlineData("'unterminated == 'Debug'")]
        [InlineData("'notABoolean'")]
        [InlineData("'a' > 'b'")]
        public void Unsupported_expression_is_reported_as_unevaluatable(string condition)
        {
            var isEvaluated = ConditionEvaluator.TryEvaluate(
                condition,
                new MSBuildProperties(null, null),
                out var result);

            Assert.False(isEvaluated);

            // An unevaluatable condition must never drop the items it guards.
            Assert.True(result);
        }

        private static bool Evaluate(string condition, IReadOnlyDictionary<string, string> globalProperties = null)
        {
            var properties = new MSBuildProperties(globalProperties, null);

            Assert.True(
                ConditionEvaluator.TryEvaluate(condition, properties, out var result),
                $"The condition `{condition}` was not evaluated.");

            return result;
        }
    }
}
