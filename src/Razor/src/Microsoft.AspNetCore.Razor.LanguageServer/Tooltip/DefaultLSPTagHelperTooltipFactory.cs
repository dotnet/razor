﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Tooltip
{
    internal class DefaultLSPTagHelperTooltipFactory : LSPTagHelperTooltipFactory
    {
        public override bool TryCreateTooltip(
            AggregateBoundElementDescription elementDescriptionInfo,
            MarkupKind markupKind,
            [NotNullWhen(true)] out MarkupContent? tooltipContent)
        {
            if (elementDescriptionInfo is null)
            {
                throw new ArgumentNullException(nameof(elementDescriptionInfo));
            }

            var associatedTagHelperInfos = elementDescriptionInfo.AssociatedTagHelperDescriptions;
            if (associatedTagHelperInfos.Count == 0)
            {
                tooltipContent = null;
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
                StartOrEndBold(descriptionBuilder, markupKind);
                descriptionBuilder.Append(reducedTypeName);
                StartOrEndBold(descriptionBuilder, markupKind);

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

            tooltipContent = new MarkupContent
            {
                Kind = markupKind,
                Value = descriptionBuilder.ToString(),
            };
            return true;
        }

        public override bool TryCreateTooltip(
            AggregateBoundAttributeDescription attributeDescriptionInfo,
            MarkupKind markupKind,
            [NotNullWhen(true)] out MarkupContent? tooltipContent)
        {
            if (attributeDescriptionInfo is null)
            {
                throw new ArgumentNullException(nameof(attributeDescriptionInfo));
            }

            var associatedAttributeInfos = attributeDescriptionInfo.DescriptionInfos;
            if (associatedAttributeInfos.Count == 0)
            {
                tooltipContent = null;
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

                StartOrEndBold(descriptionBuilder, markupKind);
                if (!TypeNameStringResolver.TryGetSimpleName(descriptionInfo.ReturnTypeName, out var returnTypeName))
                {
                    returnTypeName = descriptionInfo.ReturnTypeName;
                }

                var reducedReturnTypeName = ReduceTypeName(returnTypeName);
                descriptionBuilder.Append(reducedReturnTypeName);
                StartOrEndBold(descriptionBuilder, markupKind);
                descriptionBuilder.Append(" ");
                var tagHelperTypeName = descriptionInfo.TypeName;
                var reducedTagHelperTypeName = ReduceTypeName(tagHelperTypeName);
                descriptionBuilder.Append(reducedTagHelperTypeName);
                descriptionBuilder.Append(".");
                StartOrEndBold(descriptionBuilder, markupKind);
                descriptionBuilder.Append(descriptionInfo.PropertyName);
                StartOrEndBold(descriptionBuilder, markupKind);

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

            tooltipContent = new MarkupContent
            {
                Kind = markupKind,
                Value = descriptionBuilder.ToString(),
            };
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
            var crefMatches = ExtractCrefMatches(summaryContent);
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

        private void StartOrEndBold(StringBuilder stringBuilder, MarkupKind markupKind)
        {
            if (markupKind == MarkupKind.Markdown)
            {
                stringBuilder.Append("**");
            }
        }
    }
}
