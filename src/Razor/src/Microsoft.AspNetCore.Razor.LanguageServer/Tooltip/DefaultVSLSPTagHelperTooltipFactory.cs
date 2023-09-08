﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.Tooltip;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Text.Adornments;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Tooltip;

internal class DefaultVSLSPTagHelperTooltipFactory : VSLSPTagHelperTooltipFactory
{
    private static readonly Guid s_imageCatalogGuid = new("{ae27a6b0-e345-4288-96df-5eaf394ee369}");

    // Internal for testing
    internal static readonly ImageElement ClassGlyph = new(
        new ImageId(s_imageCatalogGuid, 463), // KnownImageIds.Type = 463
        SR.TagHelper_Element_Glyph);

    // Internal for testing
    internal static readonly ImageElement PropertyGlyph = new(
        new ImageId(s_imageCatalogGuid, 2429), // KnownImageIds.Type = 2429
        SR.TagHelper_Attribute_Glyph);

    private static readonly IReadOnlyList<string> s_cSharpPrimitiveTypes =
        new string[] { "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint",
            "nint", "nuint", "long", "ulong", "short", "ushort", "object", "string", "dynamic" };

    private static readonly IReadOnlyDictionary<string, string> s_typeNameToAlias = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "Int32", "int" },
        { "Int64", "long" },
        { "Int16", "short" },
        { "Single", "float" },
        { "Double", "double" },
        { "Decimal", "decimal" },
        { "Boolean", "bool" },
        { "String", "string" },
        { "Char", "char" }
    };

    private static readonly ClassifiedTextRun s_space = new(VSPredefinedClassificationTypeNames.WhiteSpace, " ");
    private static readonly ClassifiedTextRun s_dot = new(VSPredefinedClassificationTypeNames.Punctuation, ".");
    private static readonly ClassifiedTextRun s_newLine = new(VSPredefinedClassificationTypeNames.WhiteSpace, Environment.NewLine);
    private static readonly ClassifiedTextRun s_nullableType = new(VSPredefinedClassificationTypeNames.Punctuation, "?");

    public override bool TryCreateTooltip(AggregateBoundElementDescription elementDescriptionInfo, [NotNullWhen(true)] out ContainerElement? tooltipContent)
    {
        if (elementDescriptionInfo is null)
        {
            throw new ArgumentNullException(nameof(elementDescriptionInfo));
        }

        if (!TryClassifyElement(elementDescriptionInfo, out var descriptionClassifications))
        {
            tooltipContent = null;
            return false;
        }

        tooltipContent = CombineClassifiedTextRuns(descriptionClassifications, ClassGlyph);
        return true;
    }

    public override bool TryCreateTooltip(AggregateBoundAttributeDescription attributeDescriptionInfo, [NotNullWhen(true)] out ContainerElement? tooltipContent)
    {
        if (attributeDescriptionInfo is null)
        {
            throw new ArgumentNullException(nameof(attributeDescriptionInfo));
        }

        if (!TryClassifyAttribute(attributeDescriptionInfo, out var descriptionClassifications))
        {
            tooltipContent = null;
            return false;
        }

        tooltipContent = CombineClassifiedTextRuns(descriptionClassifications, PropertyGlyph);
        return true;
    }

    // TO-DO: This method can be removed once LSP's VSCompletionItem supports returning ContainerElements for
    // its Description property, tracked by https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1319274.
    public override bool TryCreateTooltip(AggregateBoundElementDescription elementDescriptionInfo, [NotNullWhen(true)] out ClassifiedTextElement? tooltipContent)
    {
        if (elementDescriptionInfo is null)
        {
            throw new ArgumentNullException(nameof(elementDescriptionInfo));
        }

        if (!TryClassifyElement(elementDescriptionInfo, out var descriptionClassifications))
        {
            tooltipContent = null;
            return false;
        }

        tooltipContent = GenerateClassifiedTextElement(descriptionClassifications);
        return true;
    }

    // TO-DO: This method can be removed once LSP's VSCompletionItem supports returning ContainerElements for
    // its Description property, tracked by https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1319274.
    public override bool TryCreateTooltip(AggregateBoundAttributeDescription attributeDescriptionInfo, [NotNullWhen(true)] out ClassifiedTextElement? tooltipContent)
    {
        if (attributeDescriptionInfo is null)
        {
            throw new ArgumentNullException(nameof(attributeDescriptionInfo));
        }

        if (!TryClassifyAttribute(attributeDescriptionInfo, out var descriptionClassifications))
        {
            tooltipContent = null;
            return false;
        }

        tooltipContent = GenerateClassifiedTextElement(descriptionClassifications);
        return true;
    }

    private static bool TryClassifyElement(AggregateBoundElementDescription elementInfo, out ImmutableArray<DescriptionClassification> classifications)
    {
        var associatedTagHelperInfos = elementInfo.DescriptionInfos;
        if (associatedTagHelperInfos.Length == 0)
        {
            classifications = default;
            return false;
        }

        using var descriptions = new PooledArrayBuilder<DescriptionClassification>();

        // Generates a ClassifiedTextElement that looks something like:
        //     Namespace.TypeName
        //     Summary description
        // with the specific element parts classified appropriately.

        foreach (var descriptionInfo in associatedTagHelperInfos)
        {
            // 1. Classify type name
            var typeRuns = new List<ClassifiedTextRun>();
            ClassifyTypeName(typeRuns, descriptionInfo.TagHelperTypeName);

            // 2. Classify summary
            var documentationRuns = new List<ClassifiedTextRun>();
            TryClassifySummary(documentationRuns, descriptionInfo.Documentation);

            // 3. Combine type + summary information
            descriptions.Add(new DescriptionClassification(typeRuns, documentationRuns));
        }

        classifications = descriptions.DrainToImmutable();
        return true;
    }

    private static bool TryClassifyAttribute(AggregateBoundAttributeDescription attributeInfo, out ImmutableArray<DescriptionClassification> classifications)
    {
        var associatedAttributeInfos = attributeInfo.DescriptionInfos;
        if (associatedAttributeInfos.Length == 0)
        {
            classifications = default;
            return false;
        }

        using var descriptions = new PooledArrayBuilder<DescriptionClassification>();

        // Generates a ClassifiedTextElement that looks something like:
        //     ReturnType Namespace.TypeName.Property
        //     Summary description
        // with the specific element parts classified appropriately.

        foreach (var descriptionInfo in associatedAttributeInfos)
        {
            // 1. Classify type name and property
            var typeRuns = new List<ClassifiedTextRun>();

            if (!TypeNameStringResolver.TryGetSimpleName(descriptionInfo.ReturnTypeName, out var returnTypeName))
            {
                returnTypeName = descriptionInfo.ReturnTypeName;
            }

            var reducedReturnTypeName = ReduceTypeName(returnTypeName);
            ClassifyReducedTypeName(typeRuns, reducedReturnTypeName);
            typeRuns.Add(s_space);
            ClassifyTypeName(typeRuns, descriptionInfo.TypeName);
            typeRuns.Add(s_dot);
            typeRuns.Add(new ClassifiedTextRun(VSPredefinedClassificationTypeNames.Identifier, descriptionInfo.PropertyName));

            // 2. Classify summary
            var documentationRuns = new List<ClassifiedTextRun>();
            TryClassifySummary(documentationRuns, descriptionInfo.Documentation);

            // 3. Combine type + summary information
            descriptions.Add(new DescriptionClassification(typeRuns, documentationRuns));
        }

        classifications = descriptions.DrainToImmutable();
        return true;
    }

    private static void ClassifyTypeName(List<ClassifiedTextRun> runs, string tagHelperTypeName)
    {
        var reducedTypeName = ReduceTypeName(tagHelperTypeName);
        if (reducedTypeName == tagHelperTypeName)
        {
            ClassifyReducedTypeName(runs, reducedTypeName);
            return;
        }

        // If we reach this point, the type is prefixed by a namespace so we have to do a little extra work.
        var typeNameParts = tagHelperTypeName.Split('.');

        var reducedTypeIndex = Array.LastIndexOf(typeNameParts, reducedTypeName);

        for (var partIndex = 0; partIndex < typeNameParts.Length; partIndex++)
        {
            if (partIndex != 0)
            {
                runs.Add(s_dot);
            }

            var typeNamePart = typeNameParts[partIndex];

            // Only the reduced type name should be classified as non-plain text. We also need to check
            // for a matching index since other parts of the full type name may include the reduced type
            // name (e.g. Namespace.Pages.Pages).
            if (typeNamePart == reducedTypeName && partIndex == reducedTypeIndex)
            {
                ClassifyReducedTypeName(runs, typeNamePart);
            }
            else
            {
                runs.Add(new ClassifiedTextRun(VSPredefinedClassificationTypeNames.Text, typeNamePart));
            }
        }
    }

    private static void ClassifyReducedTypeName(List<ClassifiedTextRun> runs, string reducedTypeName)
    {
        using var _ = StringBuilderPool.GetPooledObject(out var currentTextRun);

        for (var i = 0; i < reducedTypeName.Length; i++)
        {
            var ch = reducedTypeName[i];

            // There are certain characters that should be classified as plain text. For example,
            // in 'TypeName<T, T2>', the characters '<', ',' and '>' should be classified as plain
            // text while the rest should be classified as a keyword or type.
            if (ch is '<' or '>' or '[' or ']' or ',')
            {
                if (currentTextRun.Length != 0)
                {
                    var currentRunTextStr = currentTextRun.ToString();

                    // The type we're working with could contain a nested type, in which case we may
                    // also need to reduce the inner type name(s), e.g. 'List<NamespaceName.TypeName>'
                    if (ch is '<' or '>' or '[' or ']' && currentRunTextStr.Contains('.'))
                    {
                        var reducedName = ReduceTypeName(currentRunTextStr);
                        ClassifyShortName(runs, reducedName);
                    }
                    else
                    {
                        ClassifyShortName(runs, currentRunTextStr);
                    }

                    currentTextRun.Clear();
                }

                runs.Add(new ClassifiedTextRun(VSPredefinedClassificationTypeNames.Punctuation, ch.ToString()));
            }
            else
            {
                currentTextRun.Append(ch);
            }
        }

        if (currentTextRun.Length != 0)
        {
            ClassifyShortName(runs, currentTextRun.ToString());
        }
    }

    private static void ClassifyShortName(List<ClassifiedTextRun> runs, string typeName)
    {
        var nullableType = typeName.EndsWith("?");
        if (nullableType)
        {
            // Classify the '?' symbol separately from the rest of the type since it's considered punctuation.
            typeName = typeName[..^1];
        }

        // Case 1: Type can be aliased as a C# built-in type (e.g. Boolean -> bool, Int32 -> int, etc.).
        if (s_typeNameToAlias.TryGetValue(typeName, out var aliasedTypeName))
        {
            runs.Add(new ClassifiedTextRun(VSPredefinedClassificationTypeNames.Keyword, aliasedTypeName));
        }
        // Case 2: Type is a C# built-in type (e.g. bool, int, etc.).
        else if (s_cSharpPrimitiveTypes.Contains(typeName))
        {
            runs.Add(new ClassifiedTextRun(VSPredefinedClassificationTypeNames.Keyword, typeName));
        }
        // Case 3: All other types.
        else
        {
            runs.Add(new ClassifiedTextRun(VSPredefinedClassificationTypeNames.Type, typeName));
        }

        if (nullableType)
        {
            runs.Add(s_nullableType);
        }
    }

    private static bool TryClassifySummary(List<ClassifiedTextRun> runs, string? documentation)
    {
        if (!TryExtractSummary(documentation, out var summaryContent))
        {
            return false;
        }

        CleanAndClassifySummaryContent(runs, summaryContent);
        return true;
    }

    // Internal for testing
    internal static void CleanAndClassifySummaryContent(List<ClassifiedTextRun> runs, string summaryContent)
    {
        // TO-DO: We currently don't handle all possible XML comment tags and should add support
        // for them in the future. Tracked by https://github.com/dotnet/aspnetcore/issues/32286.
        summaryContent = summaryContent.Trim();
        var lines = summaryContent.ToString().Split('\n').Select(line => line.Trim());
        summaryContent = string.Join(Environment.NewLine, lines);

        // There's a few edge cases we need to explicitly convert.
        summaryContent = summaryContent.Replace("&lt;", "<");
        summaryContent = summaryContent.Replace("&gt;", ">");
        summaryContent = summaryContent.Replace("<para>", Environment.NewLine);
        summaryContent = summaryContent.Replace("</para>", Environment.NewLine);

        var codeMatches = ExtractCodeMatches(summaryContent);
        var crefMatches = ExtractCrefMatches(summaryContent);

        if (codeMatches.Count == 0 && crefMatches.Count == 0)
        {
            runs.Add(new ClassifiedTextRun(VSPredefinedClassificationTypeNames.Text, summaryContent));
            return;
        }

        using var _ = StringBuilderPool.GetPooledObject(out var currentTextRun);

        var currentCrefMatchIndex = 0;
        var currentCodeMatchIndex = 0;
        for (var i = 0; i < summaryContent.Length; i++)
        {
            // If we made it through all the crefs and code matches, add the rest of the text and break out of the loop.
            if (currentCrefMatchIndex == crefMatches.Count && currentCodeMatchIndex == codeMatches.Count)
            {
                currentTextRun.Append(summaryContent[i..]);
                break;
            }

            var currentCodeMatch = currentCodeMatchIndex < codeMatches.Count ? codeMatches[currentCodeMatchIndex] : null;
            var currentCrefMatch = currentCrefMatchIndex < crefMatches.Count ? crefMatches[currentCrefMatchIndex] : null;

            if (currentCodeMatch != null && i == currentCodeMatch.Index)
            {
                ClassifyExistingTextRun(runs, currentTextRun);

                // We've processed the existing string, now we can process the code block.
                var value = currentCodeMatch.Groups[TagContentGroupName].Value;
                if (value.Length != 0)
                {
                    runs.Add(new ClassifiedTextRun(VSPredefinedClassificationTypeNames.Text, value.ToString(), ClassifiedTextRunStyle.UseClassificationFont));
                }

                i += currentCodeMatch.Length - 1;
                currentCodeMatchIndex++;
            }
            else if (currentCrefMatch != null && i == currentCrefMatch.Index)
            {
                ClassifyExistingTextRun(runs, currentTextRun);

                // We've processed the existing string, now we can process the actual cref.
                var value = currentCrefMatch.Groups[TagContentGroupName].Value;
                var reducedValue = ReduceCrefValue(value);
                reducedValue = reducedValue.Replace("{", "<").Replace("}", ">").Replace("`1", "<>");
                ClassifyTypeName(runs, reducedValue);

                i += currentCrefMatch.Length - 1;
                currentCrefMatchIndex++;
            }
            else
            {
                currentTextRun.Append(summaryContent[i]);
            }
        }

        ClassifyExistingTextRun(runs, currentTextRun);

        static void ClassifyExistingTextRun(List<ClassifiedTextRun> runs, StringBuilder currentTextRun)
        {
            if (currentTextRun.Length != 0)
            {

                runs.Add(new ClassifiedTextRun(VSPredefinedClassificationTypeNames.Text, currentTextRun.ToString()));
                currentTextRun.Clear();
            }
        }
    }

    private static ContainerElement CombineClassifiedTextRuns(IReadOnlyList<DescriptionClassification> descriptionClassifications, ImageElement glyph)
    {
        var isFirstElement = true;
        var classifiedElementContainer = new List<ContainerElement>();
        foreach (var classification in descriptionClassifications)
        {
            // Adds blank lines between multiple classified elements
            if (isFirstElement)
            {
                isFirstElement = false;
            }
            else
            {
                classifiedElementContainer.Add(new ContainerElement(ContainerElementStyle.Wrapped, new ClassifiedTextElement()));
            }

            classifiedElementContainer.Add(new ContainerElement(ContainerElementStyle.Wrapped, glyph, new ClassifiedTextElement(classification.Type)));

            if (classification.Documentation.Count > 0)
            {
                classifiedElementContainer.Add(new ContainerElement(ContainerElementStyle.Wrapped, new ClassifiedTextElement(classification.Documentation)));
            }
        }

        return new ContainerElement(ContainerElementStyle.Stacked, classifiedElementContainer);
    }

    private static ClassifiedTextElement GenerateClassifiedTextElement(ImmutableArray<DescriptionClassification> descriptionClassifications)
    {
        var runs = new List<ClassifiedTextRun>();

        foreach (var classification in descriptionClassifications)
        {
            if (runs.Count > 0)
            {
                runs.Add(s_newLine);
                runs.Add(s_newLine);
            }

            runs.AddRange(classification.Type);
            if (classification.Documentation.Count > 0)
            {
                runs.Add(s_newLine);
                runs.AddRange(classification.Documentation);
            }
        }

        return new ClassifiedTextElement(runs);
    }

    // Internal for testing
    // Adapted from VS' PredefinedClassificationTypeNames
    internal static class VSPredefinedClassificationTypeNames
    {
        public const string Identifier = "identifier";

        public const string Keyword = "keyword";

        public const string Punctuation = "punctuation";

        public const string Text = "text";

        public const string Type = "type";

        public const string WhiteSpace = "whitespace";
    }

    private record DescriptionClassification(IReadOnlyList<ClassifiedTextRun> Type, IReadOnlyList<ClassifiedTextRun> Documentation);
}
