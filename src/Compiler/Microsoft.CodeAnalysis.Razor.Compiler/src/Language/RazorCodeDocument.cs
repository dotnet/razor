// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class RazorCodeDocument
{
    public RazorSourceDocument Source { get; }
    public ImmutableArray<RazorSourceDocument> Imports { get; }
    public ItemCollection Items { get; }

    public RazorParserOptions ParserOptions { get; }
    public RazorCodeGenerationOptions CodeGenerationOptions { get; }

    public RazorFileKind FileKind => ParserOptions.FileKind;

    private IReadOnlyList<TagHelperDescriptor>? _tagHelpers;
    private ISet<TagHelperDescriptor>? _referencedTagHelpers;
    private RazorSyntaxTree? _preTagHelperSyntaxTree;
    private RazorSyntaxTree? _syntaxTree;
    private TagHelperDocumentContext? _tagHelperContext;

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

        Items = new ItemCollection();
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
    {
        result = _tagHelpers;
        return result is not null;
    }

    internal IReadOnlyList<TagHelperDescriptor>? GetTagHelpers()
        => _tagHelpers;

    internal IReadOnlyList<TagHelperDescriptor> GetRequiredTagHelpers()
        => _tagHelpers.AssumeNotNull();

    internal void SetTagHelpers(IReadOnlyList<TagHelperDescriptor>? tagHelpers)
    {
        _tagHelpers = tagHelpers;
    }

    internal bool TryGetReferencedTagHelpers([NotNullWhen(true)] out ISet<TagHelperDescriptor>? result)
    {
        result = _referencedTagHelpers;
        return result is not null;
    }

    internal ISet<TagHelperDescriptor>? GetReferencedTagHelpers()
        => _referencedTagHelpers;

    internal ISet<TagHelperDescriptor> GetRequiredReferencedTagHelpers()
        => _referencedTagHelpers.AssumeNotNull();

    internal void SetReferencedTagHelpers(ISet<TagHelperDescriptor> value)
    {
        ArgHelper.ThrowIfNull(value);
        _referencedTagHelpers = value;
    }

    internal bool TryGetPreTagHelperSyntaxTree([NotNullWhen(true)] out RazorSyntaxTree? result)
    {
        result = _preTagHelperSyntaxTree;
        return result is not null;
    }

    internal RazorSyntaxTree? GetPreTagHelperSyntaxTree()
        => _preTagHelperSyntaxTree;

    internal RazorSyntaxTree GetRequiredPreTagHelperSyntaxTree()
        => _preTagHelperSyntaxTree.AssumeNotNull();

    internal void SetPreTagHelperSyntaxTree(RazorSyntaxTree? syntaxTree)
    {
        _preTagHelperSyntaxTree = syntaxTree;
    }

    internal bool TryGetSyntaxTree([NotNullWhen(true)] out RazorSyntaxTree? result)
    {
        result = _syntaxTree;
        return result is not null;
    }

    internal RazorSyntaxTree? GetSyntaxTree()
        => _syntaxTree;

    internal RazorSyntaxTree GetRequiredSyntaxTree()
        => _syntaxTree.AssumeNotNull();

    internal void SetSyntaxTree(RazorSyntaxTree syntaxTree)
    {
        ArgHelper.ThrowIfNull(syntaxTree);
        _syntaxTree = syntaxTree;
    }

    internal bool TryGetTagHelperContext([NotNullWhen(true)] out TagHelperDocumentContext? result)
    {
        result = _tagHelperContext;
        return result is not null;
    }

    internal TagHelperDocumentContext? GetTagHelperContext()
        => _tagHelperContext;

    internal TagHelperDocumentContext GetRequiredTagHelperContext()
        => _tagHelperContext.AssumeNotNull();

    internal void SetTagHelperContext(TagHelperDocumentContext context)
    {
        ArgHelper.ThrowIfNull(context);

        _tagHelperContext = context;
    }
}
