namespace DotNet.Consolidate.Models
{
    /// <summary>
    /// A package a project declares in its own project file while also inheriting it from a
    /// <c>Directory.Build.props</c>.
    /// </summary>
    /// <remarks>
    /// Reported whatever the two versions are, including when they match: MSBuild sees a re-declared
    /// <c>Include</c> as a duplicate item either way, and a same-version copy quietly stops following the props
    /// file the next time it is bumped.
    /// </remarks>
    public class DirectoryBuildPropsOverride
    {
        public DirectoryBuildPropsOverride(
            string projectName,
            string packageId,
            Version projectVersion,
            Version directoryBuildPropsVersion,
            string directoryBuildPropsFile)
        {
            ProjectName = projectName;
            PackageId = packageId;
            ProjectVersion = projectVersion;
            DirectoryBuildPropsVersion = directoryBuildPropsVersion;
            DirectoryBuildPropsFile = directoryBuildPropsFile;
        }

        public string ProjectName { get; }

        public string PackageId { get; }

        /// <summary>
        /// Gets the version the project file declares.
        /// </summary>
        public Version ProjectVersion { get; }

        /// <summary>
        /// Gets the version the <c>Directory.Build.props</c> declares.
        /// </summary>
        public Version DirectoryBuildPropsVersion { get; }

        /// <summary>
        /// Gets the full path of the props file that was overridden, empty when it isn't known.
        /// </summary>
        public string DirectoryBuildPropsFile { get; }
    }
}
