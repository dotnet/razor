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

    private readonly TagHelperCollection? _tagHelpers;
    private readonly TagHelperCollection? _referencedTagHelpers;
    private readonly RazorSyntaxTree? _preTagHelperSyntaxTree;
    private readonly RazorSyntaxTree? _syntaxTree;
    private readonly ImmutableArray<RazorSyntaxTree> _importSyntaxTrees;
    private readonly TagHelperDocumentContext? _tagHelperContext;
    private readonly DocumentIntermediateNode? _documentNode;
    private readonly RazorCSharpDocument? _cSharpDocument;

    private readonly Lazy<RazorHtmlDocument> _htmlDocument;

    private RazorCodeDocument(
        RazorSourceDocument source,
        ImmutableArray<RazorSourceDocument> imports,
        RazorParserOptions parserOptions,
        RazorCodeGenerationOptions codeGenerationOptions,
        TagHelperCollection? tagHelpers,
        TagHelperCollection? referencedTagHelpers,
        RazorSyntaxTree? preTagHelperSyntaxTree,
        RazorSyntaxTree? syntaxTree,
        ImmutableArray<RazorSyntaxTree> importSyntaxTrees,
        TagHelperDocumentContext? tagHelperContext,
        DocumentIntermediateNode? documentNode,
        RazorCSharpDocument? cSharpDocument)
    {
        Source = source;
        Imports = imports.NullToEmpty();

        ParserOptions = parserOptions;
        CodeGenerationOptions = codeGenerationOptions;

        _tagHelpers = tagHelpers;
        _referencedTagHelpers = referencedTagHelpers;
        _preTagHelperSyntaxTree = preTagHelperSyntaxTree;
        _syntaxTree = syntaxTree;
        _importSyntaxTrees = importSyntaxTrees;
        _tagHelperContext = tagHelperContext;
        _documentNode = documentNode;
        _cSharpDocument = cSharpDocument;

        _htmlDocument = new Lazy<RazorHtmlDocument>(() => RazorHtmlWriter.GetHtmlDocument(this));
    }

    private RazorCodeDocument With(
        RazorSourceDocument? source = null,
        ImmutableArray<RazorSourceDocument>? imports = null,
        RazorParserOptions? parserOptions = null,
        RazorCodeGenerationOptions? codeGenerationOptions = null,
        TagHelperCollection? tagHelpers = null,
        TagHelperCollection? referencedTagHelpers = null,
        RazorSyntaxTree? preTagHelperSyntaxTree = null,
        RazorSyntaxTree? syntaxTree = null,
        ImmutableArray<RazorSyntaxTree>? importSyntaxTrees = null,
        TagHelperDocumentContext? tagHelperContext = null,
        DocumentIntermediateNode? documentNode = null,
        RazorCSharpDocument? cSharpDocument = null)
    {
        if (isChanged(source, Source)
            || (imports is not null)
            || isChanged(codeGenerationOptions, CodeGenerationOptions)
            || isChanged(tagHelpers, _tagHelpers)
            || isChanged(referencedTagHelpers, _referencedTagHelpers)
            || isChanged(preTagHelperSyntaxTree, _preTagHelperSyntaxTree)
            || isChanged(syntaxTree, _syntaxTree)
            || (importSyntaxTrees is not null)
            || isChanged(tagHelperContext, _tagHelperContext)
            || isChanged(documentNode, _documentNode)
            || isChanged(cSharpDocument, _cSharpDocument))
        {
            return new RazorCodeDocument(
                source ?? Source,
                imports ?? Imports,
                parserOptions ?? ParserOptions,
                codeGenerationOptions ?? CodeGenerationOptions,
                tagHelpers ?? _tagHelpers,
                referencedTagHelpers ?? _referencedTagHelpers,
                preTagHelperSyntaxTree ?? _preTagHelperSyntaxTree,
                syntaxTree ?? _syntaxTree,
                importSyntaxTrees ?? _importSyntaxTrees,
                tagHelperContext ?? _tagHelperContext,
                documentNode ?? _documentNode,
                cSharpDocument ?? _cSharpDocument);
        }
        else
        {
            return this;
        }

        static bool isChanged<T>(T? @new, T old) where T : class? => @new is not null && !ReferenceEquals(@new, old);
    }

    public static RazorCodeDocument Create(
        RazorSourceDocument source,
        RazorParserOptions? parserOptions = null,
        RazorCodeGenerationOptions? codeGenerationOptions = null)
        => Create(source, imports: [], parserOptions, codeGenerationOptions);

    public static RazorCodeDocument Create(
        RazorSourceDocument source,
        ImmutableArray<RazorSourceDocument> imports,
        RazorParserOptions? parserOptions = null,
        RazorCodeGenerationOptions? codeGenerationOptions = null)
    {
        ArgHelper.ThrowIfNull(source);

        return new RazorCodeDocument(
            source,
            imports,
            parserOptions ?? RazorParserOptions.Default,
            codeGenerationOptions ?? RazorCodeGenerationOptions.Default,
            tagHelpers: null,
            referencedTagHelpers: null,
            preTagHelperSyntaxTree: null,
            syntaxTree: null,
            importSyntaxTrees: default,
            tagHelperContext: null,
            documentNode: null,
            cSharpDocument: null);
    }

    internal bool TryGetTagHelpers([NotNullWhen(true)] out TagHelperCollection? result)
    {
        result = _tagHelpers;
        return result is not null;
    }

    internal TagHelperCollection? GetTagHelpers()
        => _tagHelpers;

    internal TagHelperCollection GetRequiredTagHelpers()
        => _tagHelpers.AssumeNotNull();

    internal RazorCodeDocument WithTagHelpers(TagHelperCollection? value)
        => With(tagHelpers: value);

    internal bool TryGetReferencedTagHelpers([NotNullWhen(true)] out TagHelperCollection? result)
    {
        result = _referencedTagHelpers;
        return result is not null;
    }

    internal TagHelperCollection? GetReferencedTagHelpers()
        => _referencedTagHelpers;

    internal TagHelperCollection GetRequiredReferencedTagHelpers()
        => _referencedTagHelpers.AssumeNotNull();

    internal RazorCodeDocument WithReferencedTagHelpers(TagHelperCollection value)
    {
        ArgHelper.ThrowIfNull(value);
        return With(referencedTagHelpers: value);
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

    internal RazorCodeDocument WithPreTagHelperSyntaxTree(RazorSyntaxTree? value)
        => With(preTagHelperSyntaxTree: value);

    internal bool TryGetSyntaxTree([NotNullWhen(true)] out RazorSyntaxTree? result)
    {
        result = _syntaxTree;
        return result is not null;
    }

    internal RazorSyntaxTree? GetSyntaxTree()
        => _syntaxTree;

    internal RazorSyntaxTree GetRequiredSyntaxTree()
        => _syntaxTree.AssumeNotNull();

    internal RazorCodeDocument WithSyntaxTree(RazorSyntaxTree value)
    {
        Debug.Assert(value is not null);
        return With(syntaxTree: value);
    }

    internal bool TryGetImportSyntaxTrees(out ImmutableArray<RazorSyntaxTree> result)
    {
        if (!_importSyntaxTrees.IsDefault)
        {
            result = _importSyntaxTrees;
            return true;
        }

        result = default;
        return false;
    }

    internal ImmutableArray<RazorSyntaxTree> GetImportSyntaxTrees()
        => _importSyntaxTrees.IsDefault ? [] : _importSyntaxTrees;

    internal RazorCodeDocument WithImportSyntaxTrees(ImmutableArray<RazorSyntaxTree> value)
    {
        Debug.Assert(!value.IsDefault);
        Debug.Assert(value.IsEmpty || value.All(static t => t is not null));

        return With(importSyntaxTrees: value);
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

    internal RazorCodeDocument WithTagHelperContext(TagHelperDocumentContext value)
    {
        Debug.Assert(value is not null);
        return With(tagHelperContext: value);
    }

    internal bool TryGetDocumentNode([NotNullWhen(true)] out DocumentIntermediateNode? result)
    {
        result = _documentNode;
        return result is not null;
    }

    internal DocumentIntermediateNode? GetDocumentNode()
        => _documentNode;

    internal DocumentIntermediateNode GetRequiredDocumentNode()
        => _documentNode.AssumeNotNull();

    internal RazorCodeDocument WithDocumentNode(DocumentIntermediateNode value)
    {
        Debug.Assert(value is not null);
        return With(documentNode: value);
    }

    internal bool TryGetCSharpDocument([NotNullWhen(true)] out RazorCSharpDocument? result)
    {
        result = _cSharpDocument;
        return result is not null;
    }

    internal RazorCSharpDocument? GetCSharpDocument()
        => _cSharpDocument;

    internal RazorCSharpDocument GetRequiredCSharpDocument()
        => _cSharpDocument.AssumeNotNull();

    internal RazorCodeDocument WithCSharpDocument(RazorCSharpDocument value)
    {
        Debug.Assert(value is not null);
        return With(cSharpDocument: value);
    }

    internal RazorHtmlDocument GetHtmlDocument() => _htmlDocument.Value;

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
        if (NamespaceComputer.TryComputeNamespace(this, fallbackToRootNamespace, considerImports, out @namespace, out namespaceSpan))
        {
            VerifyNamespace(this, fallbackToRootNamespace, considerImports, @namespace);
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
        => new(Source, Imports, ParserOptions, CodeGenerationOptions,
            tagHelpers: _tagHelpers,
            referencedTagHelpers: _referencedTagHelpers,
            preTagHelperSyntaxTree: _preTagHelperSyntaxTree,
            syntaxTree: _syntaxTree,
            importSyntaxTrees: _importSyntaxTrees,
            tagHelperContext: _tagHelperContext,
            documentNode: _documentNode,
            cSharpDocument: _cSharpDocument);
}
