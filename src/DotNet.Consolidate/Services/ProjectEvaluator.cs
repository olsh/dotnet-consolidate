using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using DotNet.Consolidate.Models;

using Version = DotNet.Consolidate.Models.Version;

namespace DotNet.Consolidate.Services
{
    /// <summary>
    /// Reads the <c>PackageReference</c> items of a project file, honouring the <c>Condition</c> attributes on
    /// property groups, properties, item groups and the references themselves.
    /// </summary>
    /// <remarks>
    /// This is a deliberately small subset of MSBuild: imports are not followed, and neither <c>Choose</c>/<c>When</c>
    /// nor item groups nested inside a <c>Target</c> are looked at. A condition that cannot be evaluated keeps the
    /// items it guards, so the tool never drops a package because it failed to understand a project file.
    /// </remarks>
    public class ProjectEvaluator
    {
        private const string PropertyGroupElementName = "PropertyGroup";

        private const string ItemGroupElementName = "ItemGroup";

        private const string PackageReferenceElementName = "PackageReference";

        private const string VersionElementName = "Version";

        private const string ConditionAttributeName = "Condition";

        private const string IncludeAttributeName = "Include";

        private readonly ConditionEvaluator _conditionEvaluator = new ConditionEvaluator();

        private readonly ILogger _logger;

        private readonly IReadOnlyDictionary<string, string>? _globalProperties;

        public ProjectEvaluator(ILogger logger, IReadOnlyDictionary<string, string>? globalProperties)
        {
            _logger = logger;
            _globalProperties = globalProperties;
        }

        public List<NuGetPackageInfo> ParsePackageReferences(XDocument document, string? projectFilePath)
        {
            var packageInfos = new List<NuGetPackageInfo>();
            var root = document.Root;
            if (root == null)
            {
                return packageInfos;
            }

            var context = new EvaluationContext(projectFilePath);
            var properties = new MSBuildProperties(_globalProperties, projectFilePath);
            EvaluateProperties(root, properties, context);

            var targetFrameworks =
                SplitTargetFrameworks(properties.GetValue(MSBuildProperties.TargetFrameworksPropertyName));
            if (targetFrameworks.Count == 0)
            {
                CollectPackageReferences(root, properties, context, packageInfos);

                return packageInfos;
            }

            // A multi-targeting project is evaluated once per target framework and the results are merged, so that
            // references guarded by `'$(TargetFramework)' == '...'` keep taking part in the consolidation check.
            foreach (var targetFramework in targetFrameworks)
            {
                var frameworkProperties = properties.ForTargetFramework(targetFramework);
                EvaluateProperties(root, frameworkProperties, context);
                CollectPackageReferences(root, frameworkProperties, context, packageInfos);
            }

            return packageInfos;
        }

        private static List<string> SplitTargetFrameworks(string targetFrameworks)
        {
            return targetFrameworks
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        private static IEnumerable<XElement> ElementsNamed(XElement parent, string localName)
        {
            return parent.Elements()
                .Where(e => e.Name.LocalName == localName);
        }

        /// <summary>
        /// Expands a <c>PackageReference</c> attribute value, keeping the literal text when a property reference
        /// could not be resolved. Substituting an empty string there would quietly discard the reference.
        /// </summary>
        private static string? ExpandItemValue(string? value, MSBuildProperties properties)
        {
            if (value == null)
            {
                return null;
            }

            return properties.TryExpand(value, out var expanded) ? expanded : value;
        }

        private void EvaluateProperties(XElement root, MSBuildProperties properties, EvaluationContext context)
        {
            foreach (var propertyGroup in ElementsNamed(root, PropertyGroupElementName))
            {
                if (!IsConditionMet(propertyGroup, properties, context))
                {
                    continue;
                }

                foreach (var property in propertyGroup.Elements())
                {
                    if (!IsConditionMet(property, properties, context))
                    {
                        continue;
                    }

                    // Properties are evaluated in document order, so a property may reference the ones above it.
                    properties.SetValue(property.Name.LocalName, properties.Expand(property.Value, out _));
                }
            }
        }

        private void CollectPackageReferences(
            XElement root,
            MSBuildProperties properties,
            EvaluationContext context,
            List<NuGetPackageInfo> packageInfos)
        {
            foreach (var itemGroup in ElementsNamed(root, ItemGroupElementName))
            {
                if (!IsConditionMet(itemGroup, properties, context))
                {
                    continue;
                }

                foreach (var reference in ElementsNamed(itemGroup, PackageReferenceElementName))
                {
                    if (!IsConditionMet(reference, properties, context))
                    {
                        continue;
                    }

                    var id = ExpandItemValue(
                        reference.Attribute(IncludeAttributeName)
                            ?.Value,
                        properties);
                    var rawVersion = reference.Attribute(VersionElementName)
                                         ?.Value
                                     ?? ElementsNamed(reference, VersionElementName)
                                         .FirstOrDefault()
                                         ?.Value;
                    var version = ExpandItemValue(rawVersion, properties);

                    if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version))
                    {
                        continue;
                    }

                    var packageVersion = new Version(version);
                    if (!context.MarkAsSeen(id, packageVersion))
                    {
                        continue;
                    }

                    packageInfos.Add(new NuGetPackageInfo(id, packageVersion, NuGetPackageReferenceType.Direct));
                }
            }
        }

        private bool IsConditionMet(XElement element, MSBuildProperties properties, EvaluationContext context)
        {
            var condition = element.Attribute(ConditionAttributeName)
                ?.Value;
            if (string.IsNullOrWhiteSpace(condition))
            {
                return true;
            }

            if (_conditionEvaluator.TryEvaluate(condition, properties, out var result))
            {
                return result;
            }

            if (context.ShouldReport(condition))
            {
                _logger.Message(
                    $"Unable to evaluate the condition \"{condition.Trim()}\" in {context.Description}. The items it guards will be included.");
            }

            return true;
        }

        /// <summary>
        /// Per-project evaluation state: which packages have already been collected (a multi-targeting project is
        /// walked more than once) and which unevaluatable conditions have already been reported.
        /// </summary>
        private sealed class EvaluationContext
        {
            private readonly HashSet<string> _reportedConditions = new HashSet<string>(StringComparer.Ordinal);

            private readonly HashSet<(string Id, string Version)> _collectedPackages =
                new HashSet<(string Id, string Version)>();

            public EvaluationContext(string? projectFilePath)
            {
                Description = projectFilePath ?? "the project file";
            }

            public string Description { get; }

            public bool MarkAsSeen(string id, Version version)
            {
                return _collectedPackages.Add((id.ToUpperInvariant(), version.NormalizedValue));
            }

            public bool ShouldReport(string condition)
            {
                return _reportedConditions.Add(condition);
            }
        }
    }
}
