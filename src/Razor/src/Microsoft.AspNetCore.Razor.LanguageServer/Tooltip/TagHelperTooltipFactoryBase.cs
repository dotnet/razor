// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Razor.Tooltip;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Tooltip
{
    internal abstract class TagHelperTooltipFactoryBase<T>
    {
        protected static readonly Lazy<Regex> ExtractCrefRegex = new(
            () => new Regex("<(see|seealso)[\\s]+cref=\"([^\">]+)\"[^>]*>", RegexOptions.Compiled, TimeSpan.FromSeconds(1)));

        private static readonly IReadOnlyList<char> NewLineChars = new char[] { '\n', '\r' };

        public abstract bool TryCreateTooltip(AggregateBoundElementDescription descriptionInfos, out T tooltipContent);

        public abstract bool TryCreateTooltip(AggregateBoundAttributeDescription descriptionInfos, out T tooltipContent);

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

            documentation = documentation.Trim(NewLineChars.ToArray());

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
    }
}
