// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Tooltip
{
    internal class DefaultTagHelperTooltipFactory : TagHelperTooltipFactory
    {
        private static readonly Lazy<Regex> ExtractCrefRegex = new(
            () => new Regex("<(see|seealso)[\\s]+cref=\"([^\">]+)\"[^>]*>", RegexOptions.Compiled, TimeSpan.FromSeconds(1)));

        private static readonly string[] CSharpBuiltInTypes =
            new string[] { "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint",
                "nint", "nuint", "long", "ulong", "short", "ushort", "object", "string", "dynamic" };

        private static readonly Dictionary<string, string> TypeNameToAlias = new()
        {
            { "Int32", "int" },
            { "Int64", "long" },
            { "Int16", "short" },
            { "Single", "float" },
            { "Double", "double" },
            { "Decimal", "decimal" },
            { "Boolean", "bool" },
            { "String", "string" },
        };

        private static readonly RazorClassifiedTextRun Space = new(PredefinedClassificationNames.WhiteSpace, " ");
        private static readonly RazorClassifiedTextRun Dot = new(PredefinedClassificationNames.PlainText, ".");
        private static readonly RazorClassifiedTextRun NewLine = new(PredefinedClassificationNames.WhiteSpace, Environment.NewLine);

        // Need to have a lazy server here because if we try to resolve the server it creates types which create a DefaultTagHelperDescriptionFactory, and we end up StackOverflowing.
        // This lazy can be avoided in the future by using an upcoming ILanguageServerSettings interface, but it doesn't exist/work yet.
        public DefaultTagHelperTooltipFactory(ClientNotifierServiceBase languageServer)
        {
            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            LanguageServer = languageServer;
        }

        public ClientNotifierServiceBase LanguageServer;

        public override bool TryCreateTooltip(AggregateBoundElementDescription elementDescriptionInfo, out MarkupContent tagHelperDescription)
        {
            var associatedTagHelperInfos = elementDescriptionInfo.AssociatedTagHelperDescriptions;
            if (associatedTagHelperInfos.Count == 0)
            {
                tagHelperDescription = null;
                return false;
            }

            // This generates a markdown description that looks like the following:
            // **SomeTagHelper**
            //
            // The Summary documentation text with `CrefTypeValues` in code.
            //
            // Additional description infos result in a triple `---` to separate the markdown entries.

            var descriptionBuilder = new StringBuilder();
            for (var i = 0; i < associatedTagHelperInfos.Count; i++)
            {
                var descriptionInfo = associatedTagHelperInfos[i];

                if (descriptionBuilder.Length > 0)
                {
                    descriptionBuilder.AppendLine();
                    descriptionBuilder.AppendLine("---");
                }

                var tagHelperType = descriptionInfo.TagHelperTypeName;
                var reducedTypeName = ReduceTypeName(tagHelperType);
                StartOrEndBold(descriptionBuilder);
                descriptionBuilder.Append(reducedTypeName);
                StartOrEndBold(descriptionBuilder);

                var documentation = descriptionInfo.Documentation;
                if (!TryExtractSummary(documentation, out var summaryContent))
                {
                    continue;
                }

                descriptionBuilder.AppendLine();
                descriptionBuilder.AppendLine();
                var finalSummaryContent = CleanSummaryContent(summaryContent);
                descriptionBuilder.Append(finalSummaryContent);
            }

            tagHelperDescription = new MarkupContent
            {
                Kind = GetMarkupKind()
            };

            tagHelperDescription.Value = descriptionBuilder.ToString();
            return true;
        }

        public override bool TryCreateTooltip(AggregateBoundAttributeDescription descriptionInfos, out MarkupContent tagHelperDescription)
        {
            var associatedAttributeInfos = descriptionInfos.DescriptionInfos;
            if (associatedAttributeInfos.Count == 0)
            {
                tagHelperDescription = null;
                return false;
            }

            // This generates a markdown description that looks like the following:
            // **ReturnTypeName** SomeTypeName.**SomeProperty**
            //
            // The Summary documentation text with `CrefTypeValues` in code.
            //
            // Additional description infos result in a triple `---` to separate the markdown entries.

            var descriptionBuilder = new StringBuilder();
            for (var i = 0; i < associatedAttributeInfos.Count; i++)
            {
                var descriptionInfo = associatedAttributeInfos[i];

                if (descriptionBuilder.Length > 0)
                {
                    descriptionBuilder.AppendLine();
                    descriptionBuilder.AppendLine("---");
                }

                StartOrEndBold(descriptionBuilder);
                if (!TypeNameStringResolver.TryGetSimpleName(descriptionInfo.ReturnTypeName, out var returnTypeName))
                {
                    returnTypeName = descriptionInfo.ReturnTypeName;
                }
                var reducedReturnTypeName = ReduceTypeName(returnTypeName);
                descriptionBuilder.Append(reducedReturnTypeName);
                StartOrEndBold(descriptionBuilder);
                descriptionBuilder.Append(" ");
                var tagHelperTypeName = descriptionInfo.TypeName;
                var reducedTagHelperTypeName = ReduceTypeName(tagHelperTypeName);
                descriptionBuilder.Append(reducedTagHelperTypeName);
                descriptionBuilder.Append(".");
                StartOrEndBold(descriptionBuilder);
                descriptionBuilder.Append(descriptionInfo.PropertyName);
                StartOrEndBold(descriptionBuilder);

                var documentation = descriptionInfo.Documentation;
                if (!TryExtractSummary(documentation, out var summaryContent))
                {
                    continue;
                }

                descriptionBuilder.AppendLine();
                descriptionBuilder.AppendLine();
                var finalSummaryContent = CleanSummaryContent(summaryContent);
                descriptionBuilder.Append(finalSummaryContent);
            }

            tagHelperDescription = new MarkupContent
            {
                Kind = GetMarkupKind()
            };

            tagHelperDescription.Value = descriptionBuilder.ToString();
            return true;
        }

        // Internal for testing
        internal static string CleanSummaryContent(string summaryContent)
        {
            // Cleans out all <see cref="..." /> and <seealso cref="..." /> elements. It's possible to
            // have additional doc comment types in the summary but none that require cleaning. For instance
            // if there's a <para> in the summary element when it's shown in the completion description window
            // it'll be serialized as html (wont show).
            summaryContent = summaryContent.Trim();
            var crefMatches = ExtractCrefRegex.Value.Matches(summaryContent);
            var summaryBuilder = new StringBuilder(summaryContent);

            for (var i = crefMatches.Count - 1; i >= 0; i--)
            {
                var cref = crefMatches[i];
                if (cref.Success)
                {
                    var value = cref.Groups[2].Value;
                    var reducedValue = ReduceCrefValue(value);
                    reducedValue = reducedValue.Replace("{", "<").Replace("}", ">");
                    summaryBuilder.Remove(cref.Index, cref.Length);
                    summaryBuilder.Insert(cref.Index, $"`{reducedValue}`");
                }
            }
            var lines = summaryBuilder.ToString().Split(new[] { '\n' }, StringSplitOptions.None).Select(line => line.Trim());
            var finalSummaryContent = string.Join(Environment.NewLine, lines);
            return finalSummaryContent;
        }

        private static readonly char[] NewLineChars = new char[] { '\n', '\r' };

        // Internal for testing
        internal static bool TryExtractSummary(string documentation, out string summary)
        {
            const string summaryStartTag = "<summary>";
            const string summaryEndTag = "</summary>";

            if (string.IsNullOrEmpty(documentation))
            {
                summary = null;
                return false;
            }

            documentation = documentation.Trim(NewLineChars);

            var summaryTagStart = documentation.IndexOf(summaryStartTag, StringComparison.OrdinalIgnoreCase);
            var summaryTagEndStart = documentation.IndexOf(summaryEndTag, StringComparison.OrdinalIgnoreCase);
            if (summaryTagStart == -1 || summaryTagEndStart == -1)
            {
                // A really wrong but cheap way to check if this is XML
                if (!documentation.StartsWith("<", StringComparison.Ordinal) && !documentation.EndsWith(">", StringComparison.Ordinal))
                {
                    // This doesn't look like a doc comment, we'll return it as-is.
                    summary = documentation;
                    return true;
                }

                summary = null;
                return false;
            }

            var summaryContentStart = summaryTagStart + summaryStartTag.Length;
            var summaryContentLength = summaryTagEndStart - summaryContentStart;

            summary = documentation.Substring(summaryContentStart, summaryContentLength);
            return true;
        }

        // Internal for testing
        internal static string ReduceCrefValue(string value)
        {
            // cref values come in the following formats:
            // Type = "T:Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName"
            // Property = "P:T:Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName.AspAction"
            // Member = "M:T:Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName.SomeMethod(System.Collections.Generic.List{System.String})"

            if (value.Length < 2)
            {
                return string.Empty;
            }

            var type = value[0];
            value = value.Substring(2);

            switch (type)
            {
                case 'T':
                    var reducedCrefType = ReduceTypeName(value);
                    return reducedCrefType;
                case 'P':
                case 'M':
                    // TypeName.MemberName
                    var reducedCrefProperty = ReduceMemberName(value);
                    return reducedCrefProperty;
            }

            return value;
        }

        // Internal for testing
        internal static string ReduceTypeName(string content) => ReduceFullName(content, reduceWhenDotCount: 1);

        // Internal for testing
        internal static string ReduceMemberName(string content) => ReduceFullName(content, reduceWhenDotCount: 2);

        private static string ReduceFullName(string content, int reduceWhenDotCount)
        {
            // Starts searching backwards and then substrings everything when it finds enough dots. i.e. 
            // ReduceFullName("Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName", 1) == "SomeTypeName"
            //
            // ReduceFullName("Microsoft.AspNetCore.SomeTagHelpers.SomeTypeName.AspAction", 2) == "SomeTypeName.AspAction"
            //
            // This is also smart enough to ignore nested dots in type generics[<>], methods[()], cref generics[{}].

            if (reduceWhenDotCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reduceWhenDotCount));
            }

            var dotsSeen = 0;
            var scope = 0;
            for (var i = content.Length - 1; i >= 0; i--)
            {
                do
                {
                    if (content[i] == '}')
                    {
                        scope++;
                    }
                    else if (content[i] == '{')
                    {
                        scope--;
                    }

                    if (scope > 0)
                    {
                        i--;
                    }
                } while (scope != 0 && i >= 0);

                if (i < 0)
                {
                    // Could not balance scope
                    return content;
                }

                do
                {
                    if (content[i] == ')')
                    {
                        scope++;
                    }
                    else if (content[i] == '(')
                    {
                        scope--;
                    }

                    if (scope > 0)
                    {
                        i--;
                    }
                } while (scope != 0 && i >= 0);

                if (i < 0)
                {
                    // Could not balance scope
                    return content;
                }

                do
                {
                    if (content[i] == '>')
                    {
                        scope++;
                    }
                    else if (content[i] == '<')
                    {
                        scope--;
                    }

                    if (scope > 0)
                    {
                        i--;
                    }
                } while (scope != 0 && i >= 0);

                if (i < 0)
                {
                    // Could not balance scope
                    return content;
                }

                if (content[i] == '.')
                {
                    dotsSeen++;
                }

                if (dotsSeen == reduceWhenDotCount)
                {
                    var piece = content.Substring(i + 1);
                    return piece;
                }
            }

            // Could not reduce name
            return content;
        }

        private void StartOrEndBold(StringBuilder stringBuilder)
        {
            if (GetMarkupKind() == MarkupKind.Markdown)
            {
                stringBuilder.Append("**");
            }
        }

        private MarkupKind GetMarkupKind()
        {
            var completionSupportedKinds = LanguageServer.ClientSettings?.Capabilities?.TextDocument?.Completion.Value?.CompletionItem?.DocumentationFormat;
            var hoverSupportedKinds = LanguageServer.ClientSettings?.Capabilities?.TextDocument?.Hover.Value?.ContentFormat;

            // For now we're assuming that if you support Markdown for either completions or hovers you support it for both.
            // If this assumption is ever untrue we'll have to start informing this class about if a request is for Hover or Completions.
            var supportedKinds = completionSupportedKinds ?? hoverSupportedKinds;

            if (supportedKinds?.Contains(MarkupKind.Markdown) ?? false)
            {
                return MarkupKind.Markdown;
            }
            else
            {
                return MarkupKind.PlainText;
            }
        }

        public override bool TryCreateTooltip(AggregateBoundElementDescription elementDescriptionInfo, out RazorClassifiedTextElement tagHelperDescription)
        {
            var associatedTagHelperInfos = elementDescriptionInfo.AssociatedTagHelperDescriptions;
            if (associatedTagHelperInfos.Count == 0)
            {
                tagHelperDescription = null;
                return false;
            }

            // Generates a ClassifiedTextElement that looks something like:
            //     Namespace.TypeName
            //     Summary description
            // with the specific element parts classified appropriately.
            // Additional entries are separated by two new lines.

            var runs = new List<RazorClassifiedTextRun>();
            foreach (var descriptionInfo in associatedTagHelperInfos)
            {
                if (runs.Count > 0)
                {
                    runs.Add(NewLine);
                    runs.Add(NewLine);
                }

                ClassifyTypeName(runs, descriptionInfo.TagHelperTypeName);
                TryClassifySummary(runs, descriptionInfo.Documentation);
            }

            tagHelperDescription = new RazorClassifiedTextElement(runs.ToArray());
            return true;
        }

        public override bool TryCreateTooltip(AggregateBoundAttributeDescription descriptionInfos, out RazorClassifiedTextElement tagHelperDescription)
        {
            var associatedAttributeInfos = descriptionInfos.DescriptionInfos;
            if (associatedAttributeInfos.Count == 0)
            {
                tagHelperDescription = null;
                return false;
            }

            // Generates a ClassifiedTextElement that looks something like:
            //     ReturnType Namespace.TypeName.Property
            //     Summary description
            // with the specific element parts classified appropriately.
            // Additional entries are separated by two new lines.

            var runs = new List<RazorClassifiedTextRun>();
            foreach (var descriptionInfo in associatedAttributeInfos)
            {
                if (runs.Count > 0)
                {
                    runs.Add(NewLine);
                    runs.Add(NewLine);
                }

                if (!TypeNameStringResolver.TryGetSimpleName(descriptionInfo.ReturnTypeName, out var returnTypeName))
                {
                    returnTypeName = descriptionInfo.ReturnTypeName;
                }

                var reducedReturnTypeName = ReduceTypeName(returnTypeName);
                ClassifyReducedTypeName(runs, reducedReturnTypeName);
                runs.Add(Space);
                ClassifyTypeName(runs, descriptionInfo.TypeName);
                runs.Add(Dot);
                runs.Add(new RazorClassifiedTextRun(PredefinedClassificationNames.Identifier, descriptionInfo.PropertyName));
                TryClassifySummary(runs, descriptionInfo.Documentation);
            }

            tagHelperDescription = new RazorClassifiedTextElement(runs.ToArray());
            return true;
        }

        private static void ClassifyTypeName(List<RazorClassifiedTextRun> runs, string tagHelperTypeName)
        {
            var reducedTypeName = ReduceTypeName(tagHelperTypeName);
            var typeNameParts = tagHelperTypeName.Split('.');

            var partIndex = 0;
            foreach (var typeNamePart in typeNameParts)
            {
                if (partIndex != 0)
                {
                    runs.Add(Dot);
                }

                // Only the reduced type name should be classified as non-plain text.
                if (typeNamePart == reducedTypeName)
                {
                    ClassifyReducedTypeName(runs, typeNamePart);
                }
                else
                {
                    runs.Add(new RazorClassifiedTextRun(PredefinedClassificationNames.PlainText, typeNamePart));
                }

                partIndex++;
            }
        }

        private static void ClassifyReducedTypeName(List<RazorClassifiedTextRun> runs, string reducedTypeName)
        {
            var currentRunText = new StringBuilder();
            foreach (var ch in reducedTypeName.ToCharArray())
            {
                // There are certain characters that should be classified as plain text. For example,
                // in 'TypeName<T, T2>', the characters '<', ',' and '>' should be classified as plain
                // text while the rest should be classified as a keyword or type.
                if (ch == '<' || ch == '>' || ch == '[' || ch == ']' || ch == '.' || ch == ',')
                {
                    if (currentRunText.Length != 0)
                    {
                        ClassifySectionAsKeywordOrType(runs, currentRunText.ToString());
                        currentRunText.Clear();
                    }

                    runs.Add(new RazorClassifiedTextRun(PredefinedClassificationNames.PlainText, ch.ToString()));
                }
                else
                {
                    currentRunText.Append(ch);
                }
            }

            if (currentRunText.Length != 0)
            {
                ClassifySectionAsKeywordOrType(runs, currentRunText.ToString());
            }
        }

        private static void ClassifySectionAsKeywordOrType(List<RazorClassifiedTextRun> runs, string typeName)
        {
            // Case 1: Type can be aliased as a C# built-in type (e.g. Boolean -> bool, Int32 -> int, etc.).
            if (TypeNameToAlias.TryGetValue(typeName, out var aliasedTypeName))
            {
                runs.Add(new RazorClassifiedTextRun(PredefinedClassificationNames.Keyword, aliasedTypeName));
            }
            // Case 2: Type is a C# built-in type (e.g. bool, int, etc.).
            else if (CSharpBuiltInTypes.Contains(typeName))
            {
                runs.Add(new RazorClassifiedTextRun(PredefinedClassificationNames.Keyword, typeName));
            }
            // Case 3: All other types.
            else
            {
                var typeNameParts = typeName.Split('.');

                var partIndex = 0;
                foreach (var typeNamePart in typeNameParts)
                {
                    if (partIndex != 0)
                    {
                        runs.Add(Dot);
                    }

                    runs.Add(new RazorClassifiedTextRun(PredefinedClassificationNames.Type, typeNamePart));
                    partIndex++;
                }
            }
        }

        private static bool TryClassifySummary(List<RazorClassifiedTextRun> runs, string documentation)
        {
            if (!TryExtractSummary(documentation, out var summaryContent))
            {
                return false;
            }

            runs.Add(NewLine);

            var finalSummaryContent = CleanSummaryContent(summaryContent);

            // There's a few edge cases we need to explicitly convert.
            finalSummaryContent = finalSummaryContent.Replace("&lt;", "<");
            finalSummaryContent = finalSummaryContent.Replace("&gt;", ">");
            finalSummaryContent = finalSummaryContent.Replace("`1`", "`<>");

            // Sections requiring additional logic to classify are between '`' characters.
            var sections = finalSummaryContent.Split('`');
            var classifyNextSection = false;
            foreach (var section in sections)
            {
                if (!classifyNextSection)
                {
                    runs.Add(new RazorClassifiedTextRun(PredefinedClassificationNames.PlainText, section));
                    classifyNextSection = true;
                }
                else
                {
                    ClassifySectionAsKeywordOrType(runs, section.ToString());
                    classifyNextSection = false;
                }
            }

            return true;
        }

        private static class PredefinedClassificationNames
        {
            public const string Keyword = "keyword";

            public const string WhiteSpace = "whitespace";

            public const string Type = "Type";

            public const string Identifier = "identifier";

            public const string PlainText = "Plain Text";
        }
    }
}
