using System.Collections.Generic;

namespace DotNet.Consolidate.Models
{
    public class ProjectInfo
    {
        public ProjectInfo(string projectName, string projectDirectory, ICollection<NuGetPackageInfo> packages)
        {
            ProjectName = projectName;
            ProjectDirectory = projectDirectory;
            Packages = packages;
        }

        public ICollection<NuGetPackageInfo> Packages { get; }

        public string ProjectName { get; }

        public string ProjectDirectory { get; }

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
