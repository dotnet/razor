// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed partial class RazorCodeDocument
{
    /// <summary>
    ///  Wraps an array to store properties associated with a <see cref="RazorCodeDocument"/>.
    /// </summary>
    private readonly struct PropertyTable
    {
        private const int Size = 10;

        private const int TagHelpersIndex = 0;
        private const int ReferencedTagHelpersIndex = 1;
        private const int PreTagHelperSyntaxTreeIndex = 2;
        private const int SyntaxTreeIndex = 3;
        private const int ImportSyntaxTreesIndex = 4;
        private const int TagHelperContextIndex = 5;
        private const int DocumentIntermediateNodeIndex = 6;
        private const int CSharpDocumentIndex = 7;
        private const int HtmlDocumentIndex = 8;
        private const int NamespaceInfoIndex = 9;

        private readonly object?[] _values;

        public PropertyTable()
        {
            _values = new object?[Size];
        }

        public Property<IReadOnlyList<TagHelperDescriptor>> TagHelpers => new(_values, TagHelpersIndex);
        public Property<ISet<TagHelperDescriptor>> ReferencedTagHelpers => new(_values, ReferencedTagHelpersIndex);
        public Property<RazorSyntaxTree> PreTagHelperSyntaxTree => new(_values, PreTagHelperSyntaxTreeIndex);
        public Property<RazorSyntaxTree> SyntaxTree => new(_values, SyntaxTreeIndex);
        public BoxedProperty<ImmutableArray<RazorSyntaxTree>> ImportSyntaxTrees => new(_values, ImportSyntaxTreesIndex);
        public Property<TagHelperDocumentContext> TagHelperContext => new(_values, TagHelperContextIndex);
        public Property<DocumentIntermediateNode> DocumentNode => new(_values, DocumentIntermediateNodeIndex);
        public Property<RazorCSharpDocument> CSharpDocument => new(_values, CSharpDocumentIndex);
        public Property<RazorHtmlDocument> HtmlDocument => new(_values, HtmlDocumentIndex);
        public BoxedProperty<(string name, SourceSpan? span)> NamespaceInfo => new(_values, NamespaceInfoIndex);

        public readonly ref struct Property<T>(object?[] values, int index)
            where T : class
        {
            public T? Value
                => (T?)values[index];

            public void SetValue(T? value)
                => values[index] = value;

            public bool TryGetValue([NotNullWhen(true)] out T? result)
            {
                result = Value;
                return result is not null;
            }

            public T RequiredValue
                => Value.AssumeNotNull();
        }

        public readonly ref struct BoxedProperty<T>(object?[] values, int index)
            where T : struct
        {
            private Property<StrongBox<T>> StrongBox => new(values, index);

            public T? Value => StrongBox.Value?.Value;

            public bool TryGetValue(out T result)
            {
                if (StrongBox.TryGetValue(out var box))
                {
                    result = box.Value;
                    return true;
                }

                result = default;
                return false;
            }

            public void SetValue(T value)
            {
                if (StrongBox.TryGetValue(out var box))
                {
                    // If we've already created a StrongBox, just update the value.
                    box.Value = value;
                }
                else
                {
                    // Otherwise, create a new StrongBox.
                    box = new(value);
                    StrongBox.SetValue(box);
                }
            }
        }
    }
}
