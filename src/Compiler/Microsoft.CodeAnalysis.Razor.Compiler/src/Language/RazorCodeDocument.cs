// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
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
    private ImmutableArray<RazorSyntaxTree>? _importSyntaxTrees;
    private TagHelperDocumentContext? _tagHelperContext;
    private DocumentIntermediateNode? _documentIntermediateNode;
    private RazorCSharpDocument? _csharpDocument;
    private RazorHtmlDocument? _htmlDocument;
    private (string name, SourceSpan? span)? _namespaceInfo;

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

    internal bool TryGetImportSyntaxTrees(out ImmutableArray<RazorSyntaxTree> result)
    {
        if (_importSyntaxTrees is { } imports)
        {
            result = imports;
            return true;
        }

        result = default;
        return false;
    }

    internal ImmutableArray<RazorSyntaxTree> GetImportSyntaxTrees()
        => _importSyntaxTrees ?? [];

    internal void SetImportSyntaxTrees(ImmutableArray<RazorSyntaxTree> syntaxTrees)
    {
        if (syntaxTrees.IsDefault)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(syntaxTrees));
            return;
        }

        _importSyntaxTrees = syntaxTrees;
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

    internal bool TryGetDocumentIntermediateNode([NotNullWhen(true)] out DocumentIntermediateNode? result)
    {
        result = _documentIntermediateNode;
        return result is not null;
    }

    internal DocumentIntermediateNode? GetDocumentIntermediateNode()
        => _documentIntermediateNode;

    internal DocumentIntermediateNode GetRequiredDocumentIntermediateNode()
        => _documentIntermediateNode.AssumeNotNull();

    internal void SetDocumentIntermediateNode(DocumentIntermediateNode node)
    {
        ArgHelper.ThrowIfNull(node);

        _documentIntermediateNode = node;
    }

    internal bool TryGetCSharpDocument([NotNullWhen(true)] out RazorCSharpDocument? result)
    {
        result = _csharpDocument;
        return result is not null;
    }

    internal RazorCSharpDocument? GetCSharpDocument()
        => _csharpDocument;

    internal RazorCSharpDocument GetRequiredCSharpDocument()
        => _csharpDocument.AssumeNotNull();

    internal void SetCSharpDocument(RazorCSharpDocument csharpDocument)
    {
        ArgHelper.ThrowIfNull(csharpDocument);

        _csharpDocument = csharpDocument;
    }

    internal RazorHtmlDocument GetHtmlDocument()
        => _htmlDocument ??= RazorHtmlWriter.GetHtmlDocument(this);

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
        if (fallbackToRootNamespace && considerImports && _namespaceInfo is var (name, span))
        {
            VerifyNamespace(this, fallbackToRootNamespace, considerImports, name);

            (@namespace, namespaceSpan) = (name, span);
            return true;
        }

        if (NamespaceComputer.TryComputeNamespace(this, fallbackToRootNamespace, considerImports, out @namespace, out namespaceSpan))
        {
            VerifyNamespace(this, fallbackToRootNamespace, considerImports, @namespace);

            _namespaceInfo = (@namespace, namespaceSpan);
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
}
