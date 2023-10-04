// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.VisualStudio.Editor.Razor.Snippets;

internal partial class XmlSnippetParser
{
    internal class ParsedXmlSnippet
    {
        public ImmutableArray<SnippetPart> Parts { get; }
        public string DefaultText { get; }

        public ParsedXmlSnippet(ImmutableArray<SnippetPart> parts)
        {
            Parts = parts;

            using var _ = StringBuilderPool.GetPooledObject(out var builder);
            foreach (var part in parts)
            {
                var textToAdd = part.DefaultText;
                builder.Append(textToAdd);
            }

            DefaultText = builder.ToString();
        }
    }

    internal abstract record SnippetPart(string DefaultText)
    {
        public virtual string GetInsertionString()
        {
            return DefaultText;
        }
    }

    internal record SnippetFieldPart(string FieldName, string DefaultText, int? EditIndex) : SnippetPart(DefaultText);

    internal record SnippetFunctionPart(string FieldName, string DefaultText, int? EditIndex, string FunctionName, string? FunctionParam)
        : SnippetFieldPart(FieldName, DefaultText, EditIndex)
    {
    }

    internal record SnippetCursorPart() : SnippetPart("$0");
    internal record SnippetStringPart(string Text) : SnippetPart(Text);
    internal record SnippetShortcutPart() : SnippetPart("$shortcut$")
    {
        public string Shortcut { get; set; } = "";

        public override string GetInsertionString()
        {
            if (string.IsNullOrEmpty(Shortcut))
            {
                throw new InvalidOperationException("Must set the Shortcut that was used before calling ToString");
            }

            return Shortcut;
        }
    }
}
