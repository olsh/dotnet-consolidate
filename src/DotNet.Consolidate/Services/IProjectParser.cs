using System.Collections.Generic;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services
{
    public interface IProjectParser
    {
        List<NuGetPackageInfo> ParsePackageConfig(string path);

        List<NuGetPackageInfo> ParseProjectFile(string path);

        /// <param name="content">The project file content.</param>
        /// <param name="projectFilePath">
        /// The path the content was read from, when there is one. It is what lets conditions in the content use
        /// <c>Exists</c> and the reserved <c>$(MSBuildProject*)</c> properties.
        /// </param>
        /// <returns>The package references that are active for the evaluated conditions.</returns>
        List<NuGetPackageInfo> ParseProjectContent(string content, string? projectFilePath = null);

        List<NuGetPackageInfo> ParsePackageConfigContent(string content);
    }
}
