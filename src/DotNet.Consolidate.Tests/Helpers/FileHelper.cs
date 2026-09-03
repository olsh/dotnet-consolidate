using System.IO;
using System.Reflection;

namespace DotNet.Consolidate.Tests.Helpers
{
    public class FileHelper
    {
        /// <summary>
        /// The on-disk sample solution, copied next to the test assembly by the test project.
        /// </summary>
        public static string TestSolutionDirectory => Path.Join(
            new FileInfo(
                Assembly.GetExecutingAssembly()
                    .Location).DirectoryName,
            "TestData",
            "TestSolution");

        public static string TestSolutionFile(string solutionFileName)
        {
            return Path.Join(TestSolutionDirectory, solutionFileName);
        }

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
