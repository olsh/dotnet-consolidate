using System.Collections.Generic;
using System.IO;
using System.Linq;

using CommandLine;
using CommandLine.Text;

using DotNet.Consolidate.Models;

namespace DotNet.Consolidate.Services
{
    /// <summary>
    /// Turns a command line the parser rejected into the tool's response.
    /// </summary>
    /// <remarks>
    /// A parse failure is the one outcome that never reaches an <see cref="IOutputWriter"/> on its own — it happens
    /// before <c>Program</c> has an <see cref="Options"/> to build one from. Keeping the handling here rather than
    /// in <c>Program</c> is what makes it testable.
    /// </remarks>
    public static class CommandLineErrorReporter
    {
        /// <remarks>
        /// This is what turns an <see cref="Error"/> into the sentence the parser itself prints. <c>error.Tag</c>
        /// would only say <c>UnknownOptionError</c>, without naming the option that was wrong.
        /// </remarks>
        private static readonly SentenceBuilder Sentences = SentenceBuilder.Create();

        /// <summary>
        /// Reports a failed parse and says whether the run should fail.
        /// </summary>
        /// <remarks>
        /// Nothing is written in the text format: the parser's <c>HelpWriter</c> has already put a readable message
        /// on stderr (see <see cref="CommandLineParserFactory"/>), and repeating it on stdout would only be worse.
        /// <c>-f json</c> is the exception — stdout owes the caller a single parseable document however the run
        /// ended, so the complaints travel in the <c>warnings</c> of the same document the argument errors in
        /// <c>Program</c> emit. The readable block stays on stderr for whoever is reading along.
        /// <para>
        /// <c>--help</c> and <c>--version</c> arrive here as errors too. They are requests that were satisfied, not
        /// failures, and they are dropped before anything else happens: <see cref="SentenceBuilder"/> has no
        /// sentence for either and throws when asked to format one.
        /// </para>
        /// </remarks>
        /// <returns><c>true</c> when the run should fail.</returns>
        public static bool Report(IEnumerable<string> args, IEnumerable<Error> errors, TextWriter output)
        {
            var failures = errors
                .Where(error => error is not HelpRequestedError and not VersionRequestedError)
                .ToList();
            if (failures.Count == 0)
            {
                return false;
            }

            if (DetectFormat(args) == OutputFormat.Json)
            {
                var writer = OutputWriterFactory.Create(
                    OutputFormat.Json,
                    output,
                    failures.Select(Sentences.FormatError).ToList());

                // Nothing was analyzed, so the document is the warnings and an empty list of solutions.
                writer.Flush();
            }

            return true;
        }

        /// <remarks>
        /// The format has to be recovered from the raw arguments: the parse failed, so there is no
        /// <see cref="Options"/> to read it from. Re-parsing against a type that declares nothing but
        /// <c>--format</c>, with everything else ignored, is what lets this survive the very failures it reports on
        /// — an unknown option, or a <c>-p</c> given twice, is not an option of this type at all.
        /// <see cref="CommandLineParserFactory.Create"/> is deliberately not reused: that parser is the strict one
        /// and writes to stderr, while a probe has to stay lenient and silent.
        /// </remarks>
        private static OutputFormat DetectFormat(IEnumerable<string> args)
        {
            using var parser = new Parser(settings =>
            {
                settings.CaseInsensitiveEnumValues = true;
                settings.IgnoreUnknownArguments = true;
                settings.HelpWriter = null;
            });

            return parser.ParseArguments<FormatOptions>(args) is Parsed<FormatOptions> parsed
                ? parsed.Value.Format
                : OutputFormat.Text;
        }

        private sealed class FormatOptions
        {
            [Option('f', "format", Required = false, Default = OutputFormat.Text)]
            public OutputFormat Format { get; init; } = OutputFormat.Text;
        }
    }
}
