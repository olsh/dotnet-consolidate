using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DotNet.Consolidate.Services
{
    /// <summary>
    /// Evaluates the subset of the MSBuild condition language that project files realistically use:
    /// string and numeric comparisons, <c>And</c>/<c>Or</c>/<c>!</c>, parentheses and the
    /// <c>Exists</c>/<c>HasTrailingSlash</c> functions.
    /// </summary>
    /// <remarks>
    /// Anything outside that subset is reported as unevaluatable rather than guessed at, so that a caller can keep
    /// the affected items instead of dropping them. Internally that is a <see langword="null"/> result travelling up
    /// the parser. Operands are evaluated eagerly, without the short-circuiting MSBuild does; that can only turn a
    /// condition unevaluatable, never flip a result.
    /// </remarks>
    public static class ConditionEvaluator
    {
        private enum TokenType
        {
            LeftParenthesis,

            RightParenthesis,

            Not,

            Equal,

            NotEqual,

            Less,

            LessOrEqual,

            Greater,

            GreaterOrEqual,

            QuotedText,

            BareText,

            End,
        }

        /// <summary>
        /// Evaluates a <c>Condition</c> attribute value.
        /// </summary>
        /// <param name="condition">The condition text. An empty or missing condition is <see langword="true"/>.</param>
        /// <param name="properties">The properties used to expand <c>$(Name)</c> references.</param>
        /// <param name="result">The evaluated result, or <see langword="true"/> when the condition is unevaluatable.</param>
        /// <returns><see langword="false"/> when the condition falls outside the supported subset.</returns>
        public static bool TryEvaluate(string? condition, MSBuildProperties properties, out bool result)
        {
            result = true;
            if (string.IsNullOrWhiteSpace(condition))
            {
                return true;
            }

            var evaluated = ConditionParser.Evaluate(condition, properties);
            if (evaluated == null)
            {
                return false;
            }

            result = evaluated.Value;

            return true;
        }

        private sealed class Token
        {
            public Token(TokenType type, string text)
            {
                Type = type;
                Text = text;
            }

            public TokenType Type { get; }

            public string Text { get; }
        }

        private sealed class ConditionParser
        {
            private const string ExistsFunctionName = "Exists";

            private const string HasTrailingSlashFunctionName = "HasTrailingSlash";

            private const string AndKeyword = "and";

            private const string OrKeyword = "or";

            private const string TrueText = "true";

            private const string FalseText = "false";

            private readonly List<Token> _tokens;

            private readonly MSBuildProperties _properties;

            private int _position;

            private ConditionParser(List<Token> tokens, MSBuildProperties properties)
            {
                _tokens = tokens;
                _properties = properties;
            }

            /// <summary>The token at the cursor, clamped to the terminating <see cref="TokenType.End"/>.</summary>
            private Token Current => _tokens[_position < _tokens.Count ? _position : _tokens.Count - 1];

            /// <returns>The result, or <see langword="null"/> when the condition cannot be evaluated.</returns>
            public static bool? Evaluate(string condition, MSBuildProperties properties)
            {
                var tokens = Tokenize(condition);

                return tokens == null ? null : new ConditionParser(tokens, properties).Parse();
            }

            private static List<Token>? Tokenize(string condition)
            {
                var tokens = new List<Token>();
                var index = 0;
                while (index < condition.Length)
                {
                    var character = condition[index];
                    if (char.IsWhiteSpace(character))
                    {
                        index++;

                        continue;
                    }

                    Token? token;
                    switch (character)
                    {
                        case '(':
                            token = new Token(TokenType.LeftParenthesis, "(");
                            index++;

                            break;
                        case ')':
                            token = new Token(TokenType.RightParenthesis, ")");
                            index++;

                            break;
                        case '\'':
                            token = ReadQuotedText(condition, ref index);

                            break;
                        case '!':
                            token = Matches(condition, index + 1, '=')
                                ? new Token(TokenType.NotEqual, "!=")
                                : new Token(TokenType.Not, "!");
                            index += token.Text.Length;

                            break;
                        case '=':
                            if (!Matches(condition, index + 1, '='))
                            {
                                return null;
                            }

                            token = new Token(TokenType.Equal, "==");
                            index += 2;

                            break;
                        case '<':
                        case '>':
                            token = ReadRelationalOperator(condition, ref index);

                            break;
                        default:
                            token = ReadBareText(condition, ref index);

                            break;
                    }

                    if (token == null)
                    {
                        return null;
                    }

                    tokens.Add(token);
                }

                tokens.Add(new Token(TokenType.End, string.Empty));

                return tokens;
            }

            private static bool Matches(string condition, int index, char character)
            {
                return index < condition.Length && condition[index] == character;
            }

            private static Token? ReadQuotedText(string condition, ref int index)
            {
                var closingQuote = condition.IndexOf('\'', index + 1);
                if (closingQuote < 0)
                {
                    return null;
                }

                var text = condition.Substring(index + 1, closingQuote - index - 1);
                index = closingQuote + 1;

                return new Token(TokenType.QuotedText, text);
            }

            private static Token ReadRelationalOperator(string condition, ref int index)
            {
                var isLess = condition[index] == '<';
                if (Matches(condition, index + 1, '='))
                {
                    index += 2;

                    return new Token(isLess ? TokenType.LessOrEqual : TokenType.GreaterOrEqual, isLess ? "<=" : ">=");
                }

                index++;

                return new Token(isLess ? TokenType.Less : TokenType.Greater, isLess ? "<" : ">");
            }

            private static Token? ReadBareText(string condition, ref int index)
            {
                var start = index;
                while (index < condition.Length)
                {
                    var character = condition[index];

                    // A `$(...)` chunk is consumed whole, so that the parentheses of a property function don't get
                    // mistaken for grouping parentheses.
                    if (character == '$' && Matches(condition, index + 1, '('))
                    {
                        var end = SkipBalancedParentheses(condition, index + 1);
                        if (end < 0)
                        {
                            return null;
                        }

                        index = end;

                        continue;
                    }

                    if (char.IsWhiteSpace(character) || "()<>=!'".IndexOf(character) >= 0)
                    {
                        break;
                    }

                    index++;
                }

                return index == start ? null : new Token(TokenType.BareText, condition.Substring(start, index - start));
            }

            /// <returns>The index just past the matching parenthesis, or -1 when it is unbalanced.</returns>
            private static int SkipBalancedParentheses(string condition, int openingParenthesis)
            {
                var depth = 0;
                for (var i = openingParenthesis; i < condition.Length; i++)
                {
                    if (condition[i] == '(')
                    {
                        depth++;
                    }
                    else if (condition[i] == ')')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return i + 1;
                        }
                    }
                }

                return -1;
            }

            /// <remarks>
            /// Deliberately <see cref="decimal"/> rather than <see cref="double"/>: the values are literals out of a
            /// project file, and they are compared for exact equality.
            /// </remarks>
            private static bool TryParseNumber(string text, out decimal number)
            {
                number = 0;
                if (string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }

                var trimmed = text.Trim();
                if (!trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    return decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
                }

                if (!long.TryParse(
                        trimmed.Substring(2),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out var hexValue))
                {
                    return false;
                }

                number = hexValue;

                return true;
            }

            private static bool? ToBoolean(string text)
            {
                if (string.Equals(text, TrueText, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(text, "on", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (string.Equals(text, FalseText, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(text, "off", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(text, "no", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return null;
            }

            private static bool? Compare(TokenType comparison, string left, string right)
            {
                var isLeftNumber = TryParseNumber(left, out var leftNumber);
                var isRightNumber = TryParseNumber(right, out var rightNumber);

                if (comparison == TokenType.Equal || comparison == TokenType.NotEqual)
                {
                    var areEqual = isLeftNumber && isRightNumber
                        ? leftNumber == rightNumber
                        : string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

                    return comparison == TokenType.Equal ? areEqual : !areEqual;
                }

                if (!isLeftNumber || !isRightNumber)
                {
                    return null;
                }

                return comparison switch
                {
                    TokenType.Less => leftNumber < rightNumber,
                    TokenType.LessOrEqual => leftNumber <= rightNumber,
                    TokenType.Greater => leftNumber > rightNumber,
                    TokenType.GreaterOrEqual => leftNumber >= rightNumber,
                    _ => null,
                };
            }

            private bool? Parse()
            {
                var result = ParseOr();

                return result == null || Current.Type != TokenType.End ? null : result;
            }

            private bool? ParseOr()
            {
                var result = ParseAnd();
                while (result != null && IsKeyword(OrKeyword))
                {
                    _position++;
                    var right = ParseAnd();
                    if (right == null)
                    {
                        return null;
                    }

                    result = result.Value || right.Value;
                }

                return result;
            }

            private bool? ParseAnd()
            {
                var result = ParseUnary();
                while (result != null && IsKeyword(AndKeyword))
                {
                    _position++;
                    var right = ParseUnary();
                    if (right == null)
                    {
                        return null;
                    }

                    result = result.Value && right.Value;
                }

                return result;
            }

            private bool? ParseUnary()
            {
                if (Current.Type != TokenType.Not)
                {
                    return ParsePrimary();
                }

                _position++;
                var value = ParseUnary();

                return value == null ? null : !value.Value;
            }

            private bool? ParsePrimary()
            {
                if (Current.Type == TokenType.LeftParenthesis)
                {
                    _position++;
                    var grouped = ParseOr();

                    return grouped == null || !Consume(TokenType.RightParenthesis) ? null : grouped;
                }

                var left = ParseOperand();
                if (left == null)
                {
                    return null;
                }

                var comparison = Current.Type;
                if (comparison is not (TokenType.Equal or TokenType.NotEqual or TokenType.Less or TokenType.LessOrEqual
                    or TokenType.Greater or TokenType.GreaterOrEqual))
                {
                    return ToBoolean(left);
                }

                _position++;
                var right = ParseOperand();

                return right == null ? null : Compare(comparison, left, right);
            }

            /// <summary>
            /// Reads one operand and returns its expanded text. Function calls produce <c>true</c>/<c>false</c>, so
            /// that they can be used both as a standalone boolean and as one side of a comparison.
            /// </summary>
            private string? ParseOperand()
            {
                var token = Current;
                if (token.Type == TokenType.QuotedText)
                {
                    _position++;

                    return Expand(token.Text);
                }

                if (token.Type != TokenType.BareText)
                {
                    return null;
                }

                _position++;

                return Current.Type == TokenType.LeftParenthesis ? ParseFunctionCall(token.Text) : Expand(token.Text);
            }

            /// <summary>
            /// Reads the argument list of a supported function and returns <c>true</c>/<c>false</c> as text, so that
            /// the call can be used both as a standalone boolean and as one side of a comparison.
            /// </summary>
            private string? ParseFunctionCall(string functionName)
            {
                _position++;
                var argument = ParseOperand();
                if (argument == null || !Consume(TokenType.RightParenthesis))
                {
                    return null;
                }

                if (string.Equals(functionName, ExistsFunctionName, StringComparison.OrdinalIgnoreCase))
                {
                    var exists = Exists(argument);
                    if (exists == null)
                    {
                        return null;
                    }

                    return exists.Value ? TrueText : FalseText;
                }

                if (string.Equals(functionName, HasTrailingSlashFunctionName, StringComparison.OrdinalIgnoreCase))
                {
                    return argument.EndsWith('/') || argument.EndsWith('\\') ? TrueText : FalseText;
                }

                return null;
            }

            private bool? Exists(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return false;
                }

                var projectDirectory = _properties.ProjectDirectory;
                if (projectDirectory == null)
                {
                    // Without a project file on disk there is nothing to resolve a relative path against.
                    return null;
                }

                try
                {
                    var normalized = PathUtils.EnsureSystemSeparator(path);
                    var fullPath = Path.IsPathRooted(normalized)
                        ? normalized
                        : Path.Combine(projectDirectory, normalized);

                    return File.Exists(fullPath) || Directory.Exists(fullPath);
                }
                catch (ArgumentException)
                {
                    return false;
                }
                catch (IOException)
                {
                    return false;
                }
            }

            private string? Expand(string text)
            {
                var expanded = _properties.Expand(text, out var hasUnsupportedSyntax);

                return hasUnsupportedSyntax ? null : expanded;
            }

            private bool IsKeyword(string keyword)
            {
                return Current.Type == TokenType.BareText
                       && string.Equals(Current.Text, keyword, StringComparison.OrdinalIgnoreCase);
            }

            private bool Consume(TokenType type)
            {
                if (Current.Type != type)
                {
                    return false;
                }

                _position++;

                return true;
            }
        }
    }
}
