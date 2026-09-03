using System;

using CommandLine;

namespace DotNet.Consolidate.Services
{
    public static class CommandLineParserFactory
    {
        /// <summary>
        /// Builds the parser the tool runs on.
        /// </summary>
        /// <remarks>
        /// <c>CaseInsensitiveEnumValues</c> is what lets <c>-f json</c> work and not just <c>-f Json</c>.
        /// <c>HelpWriter</c> has to be set explicitly: <see cref="Parser.Default"/> points it at
        /// <see cref="Console.Error"/>, while the <see cref="ParserSettings"/> constructor leaves it null, which
        /// silently turns <c>--help</c> and <c>--version</c> into no-ops.
        /// </remarks>
        public static Parser Create()
        {
            return new Parser(settings =>
            {
                settings.CaseInsensitiveEnumValues = true;
                settings.HelpWriter = Console.Error;
            });
        }
    }
}
