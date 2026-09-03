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
    /// the affected items instead of dropping them. Operands are evaluated eagerly, without the short-circuiting
    /// MSBuild does; that can only turn a condition unevaluatable, never flip a result.
    /// </remarks>
    public class ConditionEvaluator
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
        public bool TryEvaluate(string? condition, MSBuildProperties properties, out bool result)
        {
            result = true;
            if (string.IsNullOrWhiteSpace(condition))
            {
                return true;
            }

            try
            {
                result = new ConditionParser(condition!, properties).Evaluate();

                return true;
            }
            catch (UnsupportedConditionException)
            {
                result = true;

                return false;
            }
        }

        private sealed class UnsupportedConditionException : Exception
        {
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

            private const string TrueText = "true";

            private const string FalseText = "false";

            private readonly List<Token> _tokens;

            private readonly MSBuildProperties _properties;

            private int _position;

            public ConditionParser(string condition, MSBuildProperties properties)
            {
                _properties = properties;
                _tokens = Tokenize(condition);
            }

            private Token Current => _tokens[_position];

            public bool Evaluate()
            {
                var result = ParseOr();
                Expect(TokenType.End);

                return result;
            }

            private static List<Token> Tokenize(string condition)
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

                    switch (character)
                    {
                        case '(':
                            tokens.Add(new Token(TokenType.LeftParenthesis, "("));
                            index++;

                            continue;
                        case ')':
                            tokens.Add(new Token(TokenType.RightParenthesis, ")"));
                            index++;

                            continue;
                        case '\'':
                            tokens.Add(ReadQuotedText(condition, ref index));

                            continue;
                        case '!':
                            tokens.Add(
                                Matches(condition, index + 1, '=')
                                    ? new Token(TokenType.NotEqual, "!=")
                                    : new Token(TokenType.Not, "!"));
                            index += tokens[^1].Text.Length;

                            continue;
                        case '=':
                            if (!Matches(condition, index + 1, '='))
                            {
                                throw new UnsupportedConditionException();
                            }

                            tokens.Add(new Token(TokenType.Equal, "=="));
                            index += 2;

                            continue;
                        case '<':
                        case '>':
                            tokens.Add(ReadRelationalOperator(condition, ref index));

                            continue;
                        default:
                            tokens.Add(ReadBareText(condition, ref index));

                            continue;
                    }
                }

                tokens.Add(new Token(TokenType.End, string.Empty));

                return tokens;
            }

            private static bool Matches(string condition, int index, char character)
            {
                return index < condition.Length && condition[index] == character;
            }

            private static Token ReadQuotedText(string condition, ref int index)
            {
                var closingQuote = condition.IndexOf('\'', index + 1);
                if (closingQuote < 0)
                {
                    throw new UnsupportedConditionException();
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

            private static Token ReadBareText(string condition, ref int index)
            {
                var start = index;
                while (index < condition.Length)
                {
                    var character = condition[index];

                    // A `$(...)` chunk is consumed whole, so that the parentheses of a property function don't get
                    // mistaken for grouping parentheses.
                    if (character == '$' && Matches(condition, index + 1, '('))
                    {
                        index = SkipBalancedParentheses(condition, index + 1);

                        continue;
                    }

                    if (char.IsWhiteSpace(character) || "()<>=!'".IndexOf(character) >= 0)
                    {
                        break;
                    }

                    index++;
                }

                if (index == start)
                {
                    throw new UnsupportedConditionException();
                }

                return new Token(TokenType.BareText, condition.Substring(start, index - start));
            }

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

                throw new UnsupportedConditionException();
            }

            private static bool TryParseNumber(string text, out double number)
            {
                number = 0;
                if (string.IsNullOrWhiteSpace(text))
                {
                    return false;
                }

                var trimmed = text.Trim();
                if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        number = Convert.ToInt64(trimmed.Substring(2), 16);

                        return true;
                    }
                    catch (FormatException)
                    {
                        return false;
                    }
                    catch (OverflowException)
                    {
                        return false;
                    }
                }

                return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
            }

            private static bool ToBoolean(string text)
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

                throw new UnsupportedConditionException();
            }

            private static bool Compare(TokenType comparison, string left, string right)
            {
                if (comparison == TokenType.Equal || comparison == TokenType.NotEqual)
                {
                    var areEqual = TryParseNumber(left, out var leftNumber) &&
                                   TryParseNumber(right, out var rightNumber)
                        ? leftNumber.Equals(rightNumber)
                        : string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

                    return comparison == TokenType.Equal ? areEqual : !areEqual;
                }

                if (!TryParseNumber(left, out var first) || !TryParseNumber(right, out var second))
                {
                    throw new UnsupportedConditionException();
                }

                return comparison switch
                {
                    TokenType.Less => first < second,
                    TokenType.LessOrEqual => first <= second,
                    TokenType.Greater => first > second,
                    TokenType.GreaterOrEqual => first >= second,
                    _ => throw new UnsupportedConditionException(),
                };
            }

            private bool ParseOr()
            {
                var result = ParseAnd();
                while (IsKeyword("or"))
                {
                    _position++;
                    result = ParseAnd() || result;
                }

                return result;
            }

            private bool ParseAnd()
            {
                var result = ParseUnary();
                while (IsKeyword("and"))
                {
                    _position++;
                    result = ParseUnary() && result;
                }

                return result;
            }

            private bool ParseUnary()
            {
                if (Current.Type == TokenType.Not)
                {
                    _position++;

                    return !ParseUnary();
                }

                return ParsePrimary();
            }

            private bool ParsePrimary()
            {
                if (Current.Type == TokenType.LeftParenthesis)
                {
                    _position++;
                    var result = ParseOr();
                    Expect(TokenType.RightParenthesis);

                    return result;
                }

                var left = ParseOperand();
                var comparison = Current.Type;
                if (comparison is TokenType.Equal or TokenType.NotEqual or TokenType.Less or TokenType.LessOrEqual
                    or TokenType.Greater or TokenType.GreaterOrEqual)
                {
                    _position++;

                    return Compare(comparison, left, ParseOperand());
                }

                return ToBoolean(left);
            }

            /// <summary>
            /// Reads one operand and returns its expanded text. Function calls produce <c>true</c>/<c>false</c>, so
            /// that they can be used both as a standalone boolean and as one side of a comparison.
            /// </summary>
            private string ParseOperand()
            {
                var token = Current;
                if (token.Type == TokenType.QuotedText)
                {
                    _position++;

                    return Expand(token.Text);
                }

                if (token.Type != TokenType.BareText)
                {
                    throw new UnsupportedConditionException();
                }

                _position++;
                if (Current.Type != TokenType.LeftParenthesis)
                {
                    return Expand(token.Text);
                }

                _position++;
                var argument = ParseOperand();
                Expect(TokenType.RightParenthesis);

                if (string.Equals(token.Text, ExistsFunctionName, StringComparison.OrdinalIgnoreCase))
                {
                    return Exists(argument) ? TrueText : FalseText;
                }

                if (string.Equals(token.Text, HasTrailingSlashFunctionName, StringComparison.OrdinalIgnoreCase))
                {
                    return argument.EndsWith('/') || argument.EndsWith('\\') ? TrueText : FalseText;
                }

                throw new UnsupportedConditionException();
            }

            private bool Exists(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return false;
                }

                var projectDirectory = _properties.ProjectDirectory;
                if (projectDirectory == null)
                {
                    // Without a project file on disk there is nothing to resolve a relative path against.
                    throw new UnsupportedConditionException();
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

            private string Expand(string text)
            {
                var expanded = _properties.Expand(text, out var hasUnsupportedSyntax);
                if (hasUnsupportedSyntax)
                {
                    throw new UnsupportedConditionException();
                }

                return expanded;
            }

            private bool IsKeyword(string keyword)
            {
                return Current.Type == TokenType.BareText
                       && string.Equals(Current.Text, keyword, StringComparison.OrdinalIgnoreCase);
            }

            private void Expect(TokenType type)
            {
                if (Current.Type != type)
                {
                    throw new UnsupportedConditionException();
                }

                _position++;
            }
        }
    }
}
