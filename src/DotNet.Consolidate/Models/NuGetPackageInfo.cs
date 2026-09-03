using System;

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

        /// <summary>
        /// Gets the comparer for package IDs, which NuGet treats case-insensitively — <c>Serilog</c> and
        /// <c>serilog</c> are the same package.
        /// </summary>
        /// <remarks>
        /// Every package ID comparison in the tool goes through this one so they cannot drift apart: the
        /// grouping, the <c>-p</c>/<c>-e</c> filters and the "not found in the solution" check in
        /// <see cref="Services.PackagesAnalyzer"/>, and the <c>Update</c>/<c>Remove</c> matching in
        /// <see cref="Services.ProjectEvaluator"/>. It lives here rather than in one of them because it is a
        /// property of the ID itself, and the two sides must never disagree about which IDs are the same.
        /// </remarks>
        public static StringComparer IdComparer => StringComparer.OrdinalIgnoreCase;

        public string Id { get; }

        public Version Version { get; }

        public NuGetPackageReferenceType PackageReferenceType { get; }
    }
}
