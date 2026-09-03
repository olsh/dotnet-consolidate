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

        private const string UpdateAttributeName = "Update";

        private const string RemoveAttributeName = "Remove";

        private readonly ILogger _logger;

        private readonly IReadOnlyDictionary<string, string>? _globalProperties;

        public ProjectEvaluator(ILogger logger, IReadOnlyDictionary<string, string>? globalProperties)
        {
            _logger = logger;
            _globalProperties = globalProperties;
        }

        /// <summary>
        /// The outcome of a <c>Condition</c>, kept as three states rather than a <see cref="bool"/> because
        /// "I could not understand this" has to mean different things to the three item forms.
        /// </summary>
        private enum ConditionState
        {
            Met,

            NotMet,

            Unevaluatable
        }

        public ProjectEvaluationResult ParsePackageReferences(XDocument document, string? projectFilePath)
        {
            var root = document.Root;
            if (root == null)
            {
                return Merge(new List<EvaluationPass>());
            }

            var context = new EvaluationContext(projectFilePath);
            var properties = new MSBuildProperties(_globalProperties, projectFilePath);
            EvaluateProperties(root, properties, context);

            var targetFrameworks =
                SplitList(properties.GetValue(MSBuildProperties.TargetFrameworksPropertyName));
            if (targetFrameworks.Count == 0)
            {
                return Merge(new List<EvaluationPass> { CollectPackageReferences(root, properties, context) });
            }

            // A multi-targeting project is evaluated once per target framework and the results are merged, so that
            // references guarded by `'$(TargetFramework)' == '...'` keep taking part in the consolidation check.
            var passes = new List<EvaluationPass>();
            foreach (var targetFramework in targetFrameworks)
            {
                var frameworkProperties = properties.ForTargetFramework(targetFramework);
                EvaluateProperties(root, frameworkProperties, context);
                passes.Add(CollectPackageReferences(root, frameworkProperties, context));
            }

            return Merge(passes);
        }

        /// <summary>
        /// Folds the target framework passes into one result.
        /// </summary>
        private static ProjectEvaluationResult Merge(IReadOnlyList<EvaluationPass> passes)
        {
            return new ProjectEvaluationResult(
                MergePackages(passes),
                MergeUpdates(passes),
                MergeRemovedPackageIds(passes));
        }

        /// <remarks>
        /// Unioned, deduplicated on ID and normalized version: a reference that applies to only one target
        /// framework is still restored, and still belongs in the consolidation check.
        /// </remarks>
        private static List<NuGetPackageInfo> MergePackages(IReadOnlyList<EvaluationPass> passes)
        {
            return passes
                .SelectMany(pass => pass.Packages)
                .DistinctBy(package => (package.Id.ToUpperInvariant(), package.Version.NormalizedValue))
                .ToList();
        }

        /// <remarks>
        /// Unioned like the references, but an update carries how sure of it we are: one made by every pass
        /// supersedes the inherited version, while one made by only some of them leaves that version standing
        /// beside it, because both are really restored. See
        /// <see cref="PackageVersionUpdate.ReplacesInheritedVersion"/>.
        /// </remarks>
        private static List<PackageVersionUpdate> MergeUpdates(IReadOnlyList<EvaluationPass> passes)
        {
            var certainPassCounts = passes
                .SelectMany(pass => pass.PackageUpdates)
                .Where(update => update.Value.IsCertain)
                .GroupBy(update => update.Key, NuGetPackageInfo.IdComparer)
                .ToDictionary(updates => updates.Key, updates => updates.Count(), NuGetPackageInfo.IdComparer);

            return passes
                .SelectMany(pass => pass.PackageUpdates)
                .DistinctBy(update => (update.Key.ToUpperInvariant(), update.Value.Version.NormalizedValue))
                .Select(update => new PackageVersionUpdate(
                    update.Key,
                    update.Value.Version,
                    certainPassCounts.TryGetValue(update.Key, out var certain) && certain == passes.Count))
                .ToList();
        }

        /// <remarks>
        /// <b>Intersected</b>, where the other two are unioned: a package dropped for one target framework and
        /// kept for another is still referenced by the project.
        /// </remarks>
        private static IReadOnlyCollection<string> MergeRemovedPackageIds(IReadOnlyList<EvaluationPass> passes)
        {
            HashSet<string>? removedPackageIds = null;
            foreach (var pass in passes)
            {
                if (removedPackageIds == null)
                {
                    removedPackageIds = new HashSet<string>(pass.RemovedPackageIds, NuGetPackageInfo.IdComparer);
                }
                else
                {
                    removedPackageIds.IntersectWith(pass.RemovedPackageIds);
                }
            }

            return removedPackageIds ?? new HashSet<string>(NuGetPackageInfo.IdComparer);
        }

        /// <summary>
        /// Splits a semicolon separated MSBuild list — a target framework list, or an item identity naming
        /// several packages at once.
        /// </summary>
        private static List<string> SplitList(string value)
        {
            return value
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        private static IEnumerable<XElement> ElementsNamed(XElement parent, string localName)
        {
            return parent.Elements()
                .Where(e => e.Name.LocalName == localName);
        }

        /// <summary>
        /// Expands a <c>PackageReference</c> item identity, keeping the literal text when a property reference
        /// could not be resolved. Substituting an empty string there would quietly discard the reference; the
        /// literal simply matches no package, which is harmless.
        /// </summary>
        private static List<string> ExpandItemIdentity(string? value, MSBuildProperties properties)
        {
            if (value == null)
            {
                return new List<string>();
            }

            return SplitList(properties.TryExpand(value, out var expanded) ? expanded : value);
        }

        private static string? ReadRawVersion(XElement reference)
        {
            return reference.Attribute(VersionElementName)
                       ?.Value
                   ?? ElementsNamed(reference, VersionElementName)
                       .FirstOrDefault()
                       ?.Value;
        }

        private static ConditionState Combine(ConditionState itemGroup, ConditionState element)
        {
            if (itemGroup == ConditionState.NotMet || element == ConditionState.NotMet)
            {
                return ConditionState.NotMet;
            }

            if (itemGroup == ConditionState.Unevaluatable || element == ConditionState.Unevaluatable)
            {
                return ConditionState.Unevaluatable;
            }

            return ConditionState.Met;
        }

        private void EvaluateProperties(XElement root, MSBuildProperties properties, EvaluationContext context)
        {
            foreach (var propertyGroup in ElementsNamed(root, PropertyGroupElementName))
            {
                if (EvaluateCondition(propertyGroup, properties, context) == ConditionState.NotMet)
                {
                    continue;
                }

                foreach (var property in propertyGroup.Elements())
                {
                    if (EvaluateCondition(property, properties, context) == ConditionState.NotMet)
                    {
                        continue;
                    }

                    // Properties are evaluated in document order, so a property may reference the ones above it.
                    properties.SetValue(property.Name.LocalName, properties.Expand(property.Value, out _));
                }
            }
        }

        private EvaluationPass CollectPackageReferences(
            XElement root,
            MSBuildProperties properties,
            EvaluationContext context)
        {
            var pass = new EvaluationPass();
            foreach (var itemGroup in ElementsNamed(root, ItemGroupElementName))
            {
                var itemGroupState = EvaluateCondition(itemGroup, properties, context);
                if (itemGroupState == ConditionState.NotMet)
                {
                    continue;
                }

                foreach (var reference in ElementsNamed(itemGroup, PackageReferenceElementName))
                {
                    var state = Combine(itemGroupState, EvaluateCondition(reference, properties, context));
                    if (state == ConditionState.NotMet)
                    {
                        continue;
                    }

                    ApplyPackageReference(reference, properties, context, pass, state == ConditionState.Met);
                }
            }

            return pass;
        }

        /// <summary>
        /// Applies one <c>PackageReference</c> element to the pass being evaluated.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The three forms are mutually exclusive on an item element and mean different things: <c>Include</c>
        /// adds a reference, while <c>Update</c> and <c>Remove</c> act on references that already exist. They
        /// are tried in the order MSBuild's own <c>LazyItemEvaluator.ProcessItemElement</c> tries them.
        /// </para>
        /// <para>
        /// Only <c>Include</c> and a versioned <c>Update</c> say anything about a version — an <c>Update</c>
        /// without one is changing metadata such as <c>PrivateAssets</c>, and must not be read as a version
        /// change.
        /// </para>
        /// <para>
        /// <paramref name="isCertain"/> is <c>false</c> when a condition guarding the element could not be
        /// evaluated. An <c>Include</c> is kept regardless, as it always has been. A <c>Remove</c> is the one
        /// form that can make a package disappear, so an unevaluatable condition discards it outright rather
        /// than risk dropping a package the tool merely failed to understand.
        /// </para>
        /// </remarks>
        private void ApplyPackageReference(
            XElement reference,
            MSBuildProperties properties,
            EvaluationContext context,
            EvaluationPass pass,
            bool isCertain)
        {
            var include = ExpandItemIdentity(
                reference.Attribute(IncludeAttributeName)
                    ?.Value,
                properties);
            if (include.Count > 0)
            {
                var version = ReadIncludeVersion(reference, properties);
                if (!string.IsNullOrWhiteSpace(version))
                {
                    include.ForEach(id => pass.Include(id, new Version(version)));
                }

                return;
            }

            var remove = ExpandItemIdentity(
                reference.Attribute(RemoveAttributeName)
                    ?.Value,
                properties);
            if (remove.Count > 0)
            {
                if (isCertain)
                {
                    remove.ForEach(pass.Remove);
                }

                return;
            }

            var update = ExpandItemIdentity(
                reference.Attribute(UpdateAttributeName)
                    ?.Value,
                properties);
            if (update.Count > 0)
            {
                var version = ReadUpdateVersion(reference, properties, context);
                if (version != null)
                {
                    update.ForEach(id => pass.Update(id, version, isCertain));
                }
            }
        }

        /// <summary>
        /// Reads the version of an <c>Include</c>, keeping the literal text of a property that could not be
        /// resolved rather than discarding the reference along with it.
        /// </summary>
        private static string? ReadIncludeVersion(XElement reference, MSBuildProperties properties)
        {
            var rawVersion = ReadRawVersion(reference);
            if (rawVersion == null)
            {
                return null;
            }

            return properties.TryExpand(rawVersion, out var expanded) ? expanded : rawVersion;
        }

        /// <summary>
        /// Reads the version of an <c>Update</c>, which — unlike an <c>Include</c> — is dropped when a property
        /// in it could not be resolved.
        /// </summary>
        /// <remarks>
        /// The two are not symmetric because the fallbacks are not. Keeping the literal text of an
        /// <c>Include</c> is the lesser evil: the alternative is losing the reference. An <c>Update</c> has a
        /// better fallback available — leave the inherited version alone — and keeping the literal would be
        /// actively wrong, replacing a real version with the text <c>$(SomeVersion)</c> and manufacturing a
        /// discrepancy. That is not a corner case: a property defined in the <c>Directory.Build.props</c> is
        /// unresolvable here, because the two files are parsed separately.
        /// </remarks>
        private Version? ReadUpdateVersion(
            XElement reference,
            MSBuildProperties properties,
            EvaluationContext context)
        {
            var rawVersion = ReadRawVersion(reference);
            if (string.IsNullOrWhiteSpace(rawVersion))
            {
                return null;
            }

            if (!properties.TryExpand(rawVersion, out var expanded) || string.IsNullOrWhiteSpace(expanded))
            {
                if (context.ShouldReportUnresolvedUpdate(rawVersion))
                {
                    _logger.Message(
                        $"Unable to resolve the version \"{rawVersion.Trim()}\" of a package reference update in {context.Description}. The inherited version will be kept.");
                }

                return null;
            }

            return new Version(expanded);
        }

        private ConditionState EvaluateCondition(
            XElement element,
            MSBuildProperties properties,
            EvaluationContext context)
        {
            var condition = element.Attribute(ConditionAttributeName)
                ?.Value;
            if (string.IsNullOrWhiteSpace(condition))
            {
                return ConditionState.Met;
            }

            if (ConditionEvaluator.TryEvaluate(condition, properties, out var result))
            {
                return result ? ConditionState.Met : ConditionState.NotMet;
            }

            if (context.ShouldReportCondition(condition))
            {
                _logger.Message(
                    $"Unable to evaluate the condition \"{condition.Trim()}\" in {context.Description}. The items it guards will be included.");
            }

            return ConditionState.Unevaluatable;
        }

        /// <summary>
        /// Per-project evaluation state: what has already been reported, so a multi-targeting project doesn't
        /// complain about the same thing once per target framework.
        /// </summary>
        private sealed class EvaluationContext
        {
            private readonly HashSet<string> _reportedConditions = new HashSet<string>(StringComparer.Ordinal);

            private readonly HashSet<string> _reportedUpdates = new HashSet<string>(StringComparer.Ordinal);

            public EvaluationContext(string? projectFilePath)
            {
                Description = projectFilePath ?? "the project file";
            }

            public string Description { get; }

            public bool ShouldReportCondition(string condition)
            {
                return _reportedConditions.Add(condition);
            }

            public bool ShouldReportUnresolvedUpdate(string version)
            {
                return _reportedUpdates.Add(version);
            }
        }

        /// <summary>
        /// What one evaluation pass — one target framework, or the single pass of a project that doesn't
        /// multi-target — makes of the item groups, in document order.
        /// </summary>
        /// <remarks>
        /// MSBuild applies an <c>Update</c> or a <c>Remove</c> to the items declared above it and to nothing
        /// else, which is why the elements have to be folded in one at a time rather than collected and sorted
        /// out afterwards. What such an element does to the items the project <i>inherits</i> cannot be decided
        /// here at all: a <c>Directory.Build.props</c> is auto-imported at the very top of the project, so its
        /// items sit above the whole file and are parsed somewhere else entirely. Those are left in
        /// <see cref="PackageUpdates"/> and <see cref="RemovedPackageIds"/> for the analyzer to apply once both
        /// sides are known.
        /// </remarks>
        private sealed class EvaluationPass
        {
            private readonly List<NuGetPackageInfo> _packages = new List<NuGetPackageInfo>();

            private readonly Dictionary<string, PendingUpdate> _packageUpdates =
                new Dictionary<string, PendingUpdate>(NuGetPackageInfo.IdComparer);

            private readonly HashSet<string> _removedPackageIds = new HashSet<string>(NuGetPackageInfo.IdComparer);

            public IReadOnlyList<NuGetPackageInfo> Packages => _packages;

            public IReadOnlyDictionary<string, PendingUpdate> PackageUpdates => _packageUpdates;

            public IReadOnlyCollection<string> RemovedPackageIds => _removedPackageIds;

            public void Include(string id, Version version)
            {
                _packages.Add(new NuGetPackageInfo(id, version, NuGetPackageReferenceType.Direct));
            }

            public void Update(string id, Version version, bool isCertain)
            {
                for (var i = 0; i < _packages.Count; i++)
                {
                    if (NuGetPackageInfo.IdComparer.Equals(_packages[i].Id, id))
                    {
                        // The item keeps the identity its `Include` gave it, casing included; an update only
                        // changes metadata.
                        _packages[i] = new NuGetPackageInfo(
                            _packages[i].Id,
                            version,
                            NuGetPackageReferenceType.Direct);
                    }
                }

                // The last update of a package wins, as it does in MSBuild.
                _packageUpdates[id] = new PendingUpdate(version, isCertain);
            }

            public void Remove(string id)
            {
                _packages.RemoveAll(p => NuGetPackageInfo.IdComparer.Equals(p.Id, id));

                // Recorded whatever else the file does afterwards: the inherited items this would have matched
                // all sit above it, so nothing below can bring them back.
                _removedPackageIds.Add(id);
            }
        }

        private sealed class PendingUpdate
        {
            public PendingUpdate(Version version, bool isCertain)
            {
                Version = version;
                IsCertain = isCertain;
            }

            public Version Version { get; }

            public bool IsCertain { get; }
        }
    }
}
