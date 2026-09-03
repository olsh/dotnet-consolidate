using System;
using System.Collections.Generic;

namespace DotNet.Consolidate.Models
{
    public class ProjectInfo
    {
        /// <summary>
        /// A project that changes nothing about the references it inherits — anything read from a
        /// <c>packages.config</c>, and every project built in a test.
        /// </summary>
        public ProjectInfo(string projectName, string projectDirectory, ICollection<NuGetPackageInfo> packages)
            : this(
                projectName,
                projectDirectory,
                packages,
                Array.Empty<PackageVersionUpdate>(),
                Array.Empty<string>())
        {
        }

        public ProjectInfo(
            string projectName,
            string projectDirectory,
            ICollection<NuGetPackageInfo> packages,
            IReadOnlyCollection<PackageVersionUpdate> packageUpdates,
            IReadOnlyCollection<string> removedPackageIds)
        {
            ProjectName = projectName;
            ProjectDirectory = projectDirectory;
            Packages = packages;
            PackageUpdates = packageUpdates;
            RemovedPackageIds = removedPackageIds;
        }

        /// <summary>
        /// Gets the package references, exactly as they were parsed: the project's own, plus the ones appended
        /// from its <c>Directory.Build.props</c> at the version that file declares.
        /// </summary>
        /// <remarks>
        /// Deliberately raw. <see cref="PackageUpdates"/> and <see cref="RemovedPackageIds"/> are not applied
        /// here — <see cref="Services.PackagesAnalyzer.GetEffectivePackages"/> does that — because the
        /// <c>-o</c> report has to name the version the props file declares next to the one that replaced it,
        /// and there is nowhere else to recover it from once it has been overwritten.
        /// </remarks>
        public ICollection<NuGetPackageInfo> Packages { get; }

        /// <summary>
        /// Gets the versions the project file sets on references it does not declare itself, through
        /// <c>&lt;PackageReference Update="…" Version="…" /&gt;</c>.
        /// </summary>
        public IReadOnlyCollection<PackageVersionUpdate> PackageUpdates { get; }

        /// <summary>
        /// Gets the package IDs the project file drops through <c>&lt;PackageReference Remove="…" /&gt;</c>.
        /// </summary>
        public IReadOnlyCollection<string> RemovedPackageIds { get; }

        public string ProjectName { get; }

        public string ProjectDirectory { get; }

        /// <summary>
        /// Gets the full path of the project file this was read from, <c>null</c> when it isn't known.
        /// </summary>
        /// <remarks>
        /// Set by <see cref="Services.SolutionInfoProvider"/>, which already computes it to find the project.
        /// It is what identifies a project that belongs to more than one solution, so
        /// <see cref="Services.SolutionAnalyzer.PoolProjects"/> can count it once — the name can't do that job
        /// alone, since a solution is free to display two projects in one directory under the same name.
        /// </remarks>
        public string? ProjectFile { get; init; }

        /// <summary>
        /// Gets or sets the full path of the <c>Directory.Build.props</c> file this project inherits from,
        /// <c>null</c> when none applies.
        /// </summary>
        /// <remarks>
        /// Set by <see cref="Services.SolutionInfoProvider"/> once the nearest ancestor has been matched, the
        /// same way it fills <see cref="Packages"/> with the inherited references. Reporting an override is the
        /// only reason it is kept: a solution can have several props files, so naming the versions without
        /// naming the file they came from doesn't say where to go and change them.
        /// </remarks>
        public string? DirectoryBuildPropsFile { get; set; }
    }
}
