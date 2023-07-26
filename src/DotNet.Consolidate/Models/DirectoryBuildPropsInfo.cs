using System.Collections.Generic;

namespace DotNet.Consolidate.Models
{
    public class DirectoryBuildPropsInfo
    {
        public DirectoryBuildPropsInfo(string fileName, string directoryName, ICollection<NuGetPackageInfo> packages)
        {
            FileName = fileName;
            DirectoryName = directoryName;
            Packages = packages;
        }

        public ICollection<NuGetPackageInfo> Packages { get; }

        public string FileName { get; }

        public string DirectoryName { get; }
    }
}
