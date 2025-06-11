// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed partial class RazorCodeDocument
{
    public RazorSourceDocument Source { get; }
    public ImmutableArray<RazorSourceDocument> Imports { get; }

    public RazorParserOptions ParserOptions { get; }
    public RazorCodeGenerationOptions CodeGenerationOptions { get; }

    public RazorFileKind FileKind => ParserOptions.FileKind;

    private readonly object?[] _properties = new object?[PropertyTable.Size];
    private PropertyTable Properties => new(_properties);

    private RazorCodeDocument(
        RazorSourceDocument source,
        ImmutableArray<RazorSourceDocument> imports,
        RazorParserOptions? parserOptions,
        RazorCodeGenerationOptions? codeGenerationOptions)
    {
        Source = source;
        Imports = imports.NullToEmpty();

        ParserOptions = parserOptions ?? RazorParserOptions.Default;
        CodeGenerationOptions = codeGenerationOptions ?? RazorCodeGenerationOptions.Default;
    }

    public static RazorCodeDocument Create(
        RazorSourceDocument source,
        RazorParserOptions? parserOptions = null,
        RazorCodeGenerationOptions? codeGenerationOptions = null)
    {
        ArgHelper.ThrowIfNull(source);

        return new RazorCodeDocument(source, imports: [], parserOptions, codeGenerationOptions);
    }

    public static RazorCodeDocument Create(
        RazorSourceDocument source,
        ImmutableArray<RazorSourceDocument> imports,
        RazorParserOptions? parserOptions = null,
        RazorCodeGenerationOptions? codeGenerationOptions = null)
    {
        ArgHelper.ThrowIfNull(source);

        return new RazorCodeDocument(source, imports, parserOptions, codeGenerationOptions);
    }

    internal bool TryGetTagHelpers([NotNullWhen(true)] out IReadOnlyList<TagHelperDescriptor>? result)
        => Properties.TagHelpers.TryGetValue(out result);

    internal IReadOnlyList<TagHelperDescriptor>? GetTagHelpers()
        => Properties.TagHelpers.Value;

    internal IReadOnlyList<TagHelperDescriptor> GetRequiredTagHelpers()
        => Properties.TagHelpers.RequiredValue;

    internal void SetTagHelpers(IReadOnlyList<TagHelperDescriptor>? value)
        => Properties.TagHelpers.SetValue(value);

    internal bool TryGetReferencedTagHelpers([NotNullWhen(true)] out ISet<TagHelperDescriptor>? result)
        => Properties.ReferencedTagHelpers.TryGetValue(out result);

    internal ISet<TagHelperDescriptor>? GetReferencedTagHelpers()
        => Properties.ReferencedTagHelpers.Value;

    internal ISet<TagHelperDescriptor> GetRequiredReferencedTagHelpers()
        => Properties.ReferencedTagHelpers.RequiredValue;

    internal void SetReferencedTagHelpers(ISet<TagHelperDescriptor> value)
    {
        ArgHelper.ThrowIfNull(value);
        Properties.ReferencedTagHelpers.SetValue(value);
    }

    internal bool TryGetPreTagHelperSyntaxTree([NotNullWhen(true)] out RazorSyntaxTree? result)
        => Properties.PreTagHelperSyntaxTree.TryGetValue(out result);

    internal RazorSyntaxTree? GetPreTagHelperSyntaxTree()
        => Properties.PreTagHelperSyntaxTree.Value;

    internal RazorSyntaxTree GetRequiredPreTagHelperSyntaxTree()
        => Properties.PreTagHelperSyntaxTree.RequiredValue;

    internal void SetPreTagHelperSyntaxTree(RazorSyntaxTree? value)
        => Properties.PreTagHelperSyntaxTree.SetValue(value);

    internal bool TryGetSyntaxTree([NotNullWhen(true)] out RazorSyntaxTree? result)
        => Properties.SyntaxTree.TryGetValue(out result);

    internal RazorSyntaxTree? GetSyntaxTree()
        => Properties.SyntaxTree.Value;

    internal RazorSyntaxTree GetRequiredSyntaxTree()
        => Properties.SyntaxTree.RequiredValue;

    internal void SetSyntaxTree(RazorSyntaxTree value)
    {
        Debug.Assert(value is not null);
        Properties.SyntaxTree.SetValue(value);
    }

    internal bool TryGetImportSyntaxTrees(out ImmutableArray<RazorSyntaxTree> result)
        => Properties.ImportSyntaxTrees.TryGetValue(out result);

    internal ImmutableArray<RazorSyntaxTree> GetImportSyntaxTrees()
        => Properties.ImportSyntaxTrees.Value ?? [];

    internal void SetImportSyntaxTrees(ImmutableArray<RazorSyntaxTree> value)
    {
        Debug.Assert(!value.IsDefault);
        Debug.Assert(value.IsEmpty || value.All(static t => t is not null));

        Properties.ImportSyntaxTrees.SetValue(value);
    }

    internal bool TryGetTagHelperContext([NotNullWhen(true)] out TagHelperDocumentContext? result)
        => Properties.TagHelperContext.TryGetValue(out result);

    internal TagHelperDocumentContext? GetTagHelperContext()
        => Properties.TagHelperContext.Value;

    internal TagHelperDocumentContext GetRequiredTagHelperContext()
        => Properties.TagHelperContext.RequiredValue;

    internal void SetTagHelperContext(TagHelperDocumentContext value)
    {
        Debug.Assert(value is not null);
        Properties.TagHelperContext.SetValue(value);
    }

    internal bool TryGetDocumentNode([NotNullWhen(true)] out DocumentIntermediateNode? result)
        => Properties.DocumentNode.TryGetValue(out result);

    internal DocumentIntermediateNode? GetDocumentNode()
        => Properties.DocumentNode.Value;

    internal DocumentIntermediateNode GetRequiredDocumentNode()
        => Properties.DocumentNode.RequiredValue;

    internal void SetDocumentNode(DocumentIntermediateNode value)
    {
        Debug.Assert(value is not null);
        Properties.DocumentNode.SetValue(value);
    }

    internal bool TryGetCSharpDocument([NotNullWhen(true)] out RazorCSharpDocument? result)
        => Properties.CSharpDocument.TryGetValue(out result);

    internal RazorCSharpDocument? GetCSharpDocument()
        => Properties.CSharpDocument.Value;

    internal RazorCSharpDocument GetRequiredCSharpDocument()
        => Properties.CSharpDocument.RequiredValue;

    internal void SetCSharpDocument(RazorCSharpDocument value)
    {
        Debug.Assert(value is not null);
        Properties.CSharpDocument.SetValue(value);
    }

    internal RazorHtmlDocument GetHtmlDocument()
    {
        if (Properties.HtmlDocument.TryGetValue(out var result))
        {
            return result;
        }

        result = RazorHtmlWriter.GetHtmlDocument(this);
        Properties.HtmlDocument.SetValue(result);

        return result;
    }

    // In general documents will have a relative path (relative to the project root).
    // We can only really compute a nice namespace when we know a relative path.
    //
    // However all kinds of thing are possible in tools. We shouldn't barf here if the document isn't set up correctly.
    internal bool TryGetNamespace(
        bool fallbackToRootNamespace,
        bool considerImports,
        [NotNullWhen(true)] out string? @namespace,
        out SourceSpan? namespaceSpan)
    {
        // We only want to cache the namespace if we're considering all possibilities.
        // Anyone wanting something different (i.e., tooling) has to pay a slight penalty.
        if (fallbackToRootNamespace && considerImports &&
            Properties.NamespaceInfo.TryGetValue(out var info))
        {
            VerifyNamespace(this, fallbackToRootNamespace, considerImports, info.name);

            (@namespace, namespaceSpan) = info;
            return true;
        }

        if (NamespaceComputer.TryComputeNamespace(this, fallbackToRootNamespace, considerImports, out @namespace, out namespaceSpan))
        {
            VerifyNamespace(this, fallbackToRootNamespace, considerImports, @namespace);

            Properties.NamespaceInfo.SetValue((@namespace, namespaceSpan));
            return true;
        }

        return false;

        [Conditional("DEBUG")]
        static void VerifyNamespace(RazorCodeDocument codeDocument, bool fallbackToRootNamespace, bool considerImports, string? @namespace)
        {
            // In debug mode, even if we're cached, lets take the hit to run this again and make sure the cached value is correct.
            // This is to help us find issues with caching logic during development.
            var validateResult = NamespaceComputer.TryComputeNamespace(
                codeDocument, fallbackToRootNamespace, considerImports, out var validateNamespace, out _);

            Debug.Assert(validateResult, "We couldn't compute the namespace, but have a cached value, so something has gone wrong");
            Debug.Assert(validateNamespace == @namespace, $"We cached a namespace of {@namespace} but calculated that it should be {validateNamespace}");
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Do not use. Present to support the legacy editor", error: false)]
    internal RazorCodeDocument Clone()
    {
        var codeDocument = new RazorCodeDocument(Source, Imports, ParserOptions, CodeGenerationOptions);

        Array.Copy(_properties, codeDocument._properties, _properties.Length);

        return codeDocument;
    }
}
