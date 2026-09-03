using System.Collections.Generic;

namespace DotNet.Consolidate.Models
{
    /// <summary>
    /// What a project file said about its package references: the ones it declares, and the changes it makes
    /// to references it does not declare itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>Update</c> and <c>Remove</c> can't travel in <see cref="Packages"/>, because they are not
    /// references: MSBuild applies them to items that already exist and they add nothing of their own, so one
    /// that matches nothing has to leave no trace at all — which a package entry could not do.
    /// </para>
    /// <para>
    /// The other two collections carry what is left for the items the project <i>inherits</i>. A
    /// <c>Directory.Build.props</c> is auto-imported at the very top of the project, so its items sit above the
    /// whole file and are parsed somewhere else entirely; anything the project file does to itself has already
    /// been applied to <see cref="Packages"/>, in document order, by <see cref="Services.ProjectEvaluator"/>.
    /// </para>
    /// </remarks>
    public class ProjectEvaluationResult
    {
        public ProjectEvaluationResult(
            List<NuGetPackageInfo> packages,
            IReadOnlyCollection<PackageVersionUpdate> packageUpdates,
            IReadOnlyCollection<string> removedPackageIds)
        {
            Packages = packages;
            PackageUpdates = packageUpdates;
            RemovedPackageIds = removedPackageIds;
        }

        public List<NuGetPackageInfo> Packages { get; }

        /// <summary>
        /// Gets the versions set by a <c>&lt;PackageReference Update="…" Version="…" /&gt;</c>.
        /// </summary>
        public IReadOnlyCollection<PackageVersionUpdate> PackageUpdates { get; }

        /// <summary>
        /// Gets the package IDs dropped by a <c>&lt;PackageReference Remove="…" /&gt;</c>.
        /// </summary>
        public IReadOnlyCollection<string> RemovedPackageIds { get; }
    }
}
