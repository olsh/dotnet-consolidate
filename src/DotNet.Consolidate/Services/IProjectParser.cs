using System.Collections.Generic;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services
{
    public interface IProjectParser
    {
        List<NuGetPackageInfo> ParsePackageConfig(string path);

        List<NuGetPackageInfo> ParseProjectFile(string path);
    }
}
