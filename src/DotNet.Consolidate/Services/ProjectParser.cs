using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services
{
    public class ProjectParser : IProjectParser
    {
        public List<NuGetPackageInfo> ParsePackageConfig(string path)
        {
            var content = File.ReadAllText(path);

            return ParsePackageConfigContent(content);
        }

        public List<NuGetPackageInfo> ParsePackageConfigContent(string content)
        {
            XDocument xml = XDocument.Parse(content);
            var packageInfos = new List<NuGetPackageInfo>();
            if (xml.Root == null)
            {
                return packageInfos;
            }

            var elements = xml.Root.Elements("package");
            foreach (var element in elements)
            {
                var id = element.Attribute("id");
                var version = element.Attribute("version");

                if (id == null || version == null)
                {
                    continue;
                }

                packageInfos.Add(new NuGetPackageInfo(id.Value, new Version(version.Value)));
            }

            return packageInfos;
        }

        public List<NuGetPackageInfo> ParseProjectFile(string path)
        {
            var content = File.ReadAllText(path);

            return ParseProjectContent(content);
        }

        public List<NuGetPackageInfo> ParseProjectContent(string content)
        {
            XDocument xml = XDocument.Parse(content);
            var packageInfos = new List<NuGetPackageInfo>();
            if (xml.Root == null)
            {
                return packageInfos;
            }

            var packageReferences = xml.Root.Elements()
                .Where(e => e.Name.LocalName == "ItemGroup").Elements()
                .Where(e => e.Name.LocalName == "PackageReference");

            foreach (var reference in packageReferences)
            {
                var id = reference.Attribute("Include");
                var version = reference.Attribute("Version")?.Value ?? reference.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value;
                if (id == null || version == null)
                {
                    continue;
                }

                packageInfos.Add(new NuGetPackageInfo(id.Value, new Version(version)));
            }

            return packageInfos;
        }
    }
}
