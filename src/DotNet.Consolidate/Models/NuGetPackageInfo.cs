namespace DotNet.Consolidate.Models
{
    public class NuGetPackageInfo
    {
        public NuGetPackageInfo(string id, Version version)
        {
            Id = id;
            Version = version;
        }

        public string Id { get; }

        public Version Version { get; }
    }
}
