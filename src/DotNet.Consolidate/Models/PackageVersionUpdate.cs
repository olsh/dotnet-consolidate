namespace DotNet.Consolidate.Models
{
    /// <summary>
    /// A <c>&lt;PackageReference Update="…" Version="…" /&gt;</c> that survived the project file it was written
    /// in: the version an inherited package reference is to be set to.
    /// </summary>
    /// <remarks>
    /// Deliberately not a <see cref="NuGetPackageInfo"/>. An update is not a reference — on its own it adds
    /// nothing to the project — so a <see cref="NuGetPackageReferenceType"/> would have nothing meaningful to
    /// say about it, and an update that matches no item has to stay invisible rather than be counted as one.
    /// </remarks>
    public class PackageVersionUpdate
    {
        public PackageVersionUpdate(string id, Version version, bool replacesInheritedVersion)
        {
            Id = id;
            Version = version;
            ReplacesInheritedVersion = replacesInheritedVersion;
        }

        public string Id { get; }

        public Version Version { get; }

        /// <summary>
        /// Gets a value indicating whether the inherited version is superseded outright, rather than surviving
        /// beside this one.
        /// </summary>
        /// <remarks>
        /// <c>false</c> when the update is not certain to apply: it is guarded by a condition that could not be
        /// evaluated, or it applies to some of a multi-targeting project's frameworks and not the others — in
        /// which case both versions really are restored, one per framework. Reporting both is the same posture
        /// the tool already takes for a reference behind an unevaluatable condition: never quietly lose a
        /// version because a project file wasn't fully understood.
        /// </remarks>
        public bool ReplacesInheritedVersion { get; }
    }
}
