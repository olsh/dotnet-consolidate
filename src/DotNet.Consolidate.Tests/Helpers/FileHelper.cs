using System.IO;
using System.Reflection;

namespace DotNet.Consolidate.Tests.Helpers
{
    public class FileHelper
    {
        public static string ReadResource(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "DotNet.Consolidate.Tests.TestData." + fileName;

            using Stream stream = assembly.GetManifestResourceStream(resourceName);

            // ReSharper disable once AssignNullToNotNullAttribute
            using StreamReader reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }
    }
}
