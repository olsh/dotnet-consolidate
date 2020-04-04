namespace DotNet.Consolidate.Models
{
    public class NuGetPackageInfo
    {
        public NuGetPackageInfo(string id, string version)
        {
            Id = id;
            Version = version;
        }

        public string Id { get; }

        public string Version { get; }
    }
}
