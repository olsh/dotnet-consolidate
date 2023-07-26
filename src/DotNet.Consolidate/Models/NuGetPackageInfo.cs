namespace DotNet.Consolidate.Models
{
    public class NuGetPackageInfo
    {
        public NuGetPackageInfo(string id, Version version, NuGetPackageReferenceType packageReferenceType)
        {
            Id = id;
            Version = version;
            PackageReferenceType = packageReferenceType;
        }

        public string Id { get; }

        public Version Version { get; }

        public NuGetPackageReferenceType PackageReferenceType { get; }
    }
}
