// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

public sealed class CodeRenderingContext : IDisposable
{
    internal static readonly object NewLineString = "NewLineString";

    internal static readonly object SuppressUniqueIds = "SuppressUniqueIds";

    private readonly Stack<IntermediateNode> _ancestors;
    private readonly RazorCodeDocument _codeDocument;
    private readonly DocumentIntermediateNode _documentNode;
    private readonly List<ScopeInternal> _scopes;

    private readonly PooledObject<ImmutableArray<SourceMapping>.Builder> _sourceMappingsBuilder;

    public CodeRenderingContext(
        IntermediateNodeWriter nodeWriter,
        RazorCodeDocument codeDocument,
        DocumentIntermediateNode documentNode,
        RazorCodeGenerationOptions options)
    {
        if (nodeWriter == null)
        {
            throw new ArgumentNullException(nameof(nodeWriter));
        }

        if (codeDocument == null)
        {
            throw new ArgumentNullException(nameof(codeDocument));
        }

        if (documentNode == null)
        {
            throw new ArgumentNullException(nameof(documentNode));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _codeDocument = codeDocument;
        _documentNode = documentNode;
        Options = options;

        _ancestors = new Stack<IntermediateNode>();
        Diagnostics = new RazorDiagnosticCollection();
        Items = new ItemCollection();
        _sourceMappingsBuilder = ArrayBuilderPool<SourceMapping>.GetPooledObject();
        LinePragmas = new List<LinePragma>();

        var diagnostics = _documentNode.GetAllDiagnostics();
        for (var i = 0; i < diagnostics.Count; i++)
        {
            Diagnostics.Add(diagnostics[i]);
        }

        // Set new line character to a specific string regardless of platform, for testing purposes.
        var newLineString = codeDocument.Items[NewLineString] as string ?? Environment.NewLine;
        CodeWriter = new CodeWriter(newLineString, options);

        Items[NewLineString] = codeDocument.Items[NewLineString];
        Items[SuppressUniqueIds] = codeDocument.Items[SuppressUniqueIds] ?? options.SuppressUniqueIds;

        _scopes = new List<ScopeInternal>();
        _scopes.Add(new ScopeInternal(nodeWriter));
    }

    // This will be initialized by the document writer when the context is 'live'.
    public IntermediateNodeVisitor Visitor { get; set; }

    public IEnumerable<IntermediateNode> Ancestors => _ancestors;

    internal Stack<IntermediateNode> AncestorsInternal => _ancestors;

    public CodeWriter CodeWriter { get; }

    public RazorDiagnosticCollection Diagnostics { get; }

    public string DocumentKind => _documentNode.DocumentKind;

    public ItemCollection Items { get; }

    public ImmutableArray<SourceMapping>.Builder SourceMappings => _sourceMappingsBuilder.Object;

    internal List<LinePragma> LinePragmas { get; }

    public IntermediateNodeWriter NodeWriter => Current.Writer;

    public RazorCodeGenerationOptions Options { get; }

    public IntermediateNode Parent => _ancestors.Count == 0 ? null : _ancestors.Peek();

    public RazorSourceDocument SourceDocument => _codeDocument.Source;

    private ScopeInternal Current => _scopes[_scopes.Count - 1];

    public void AddSourceMappingFor(IntermediateNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (node.Source == null)
        {
            return;
        }

        AddSourceMappingFor(node.Source.Value);
    }

    public void AddSourceMappingFor(SourceSpan source, int offset = 0)
    {
        if (SourceDocument.FilePath != null &&
            !string.Equals(SourceDocument.FilePath, source.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            // We don't want to generate line mappings for imports.
            return;
        }

        var currentLocation = CodeWriter.Location with { AbsoluteIndex = CodeWriter.Location.AbsoluteIndex + offset, CharacterIndex = CodeWriter.Location.CharacterIndex + offset };

        var generatedLocation = new SourceSpan(currentLocation, source.Length);
        var sourceMapping = new SourceMapping(source, generatedLocation);

        SourceMappings.Add(sourceMapping);
    }

    public void RenderChildren(IntermediateNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        _ancestors.Push(node);

        for (var i = 0; i < node.Children.Count; i++)
        {
            Visitor.Visit(node.Children[i]);
        }

        _ancestors.Pop();
    }

    public void RenderChildren(IntermediateNode node, IntermediateNodeWriter writer)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (writer == null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        _scopes.Add(new ScopeInternal(writer));
        _ancestors.Push(node);

        for (var i = 0; i < node.Children.Count; i++)
        {
            Visitor.Visit(node.Children[i]);
        }

        _ancestors.Pop();
        _scopes.RemoveAt(_scopes.Count - 1);
    }

    public void RenderNode(IntermediateNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        Visitor.Visit(node);
    }

    public void RenderNode(IntermediateNode node, IntermediateNodeWriter writer)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (writer == null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        _scopes.Add(new ScopeInternal(writer));

        Visitor.Visit(node);

        _scopes.RemoveAt(_scopes.Count - 1);
    }

    public void AddLinePragma(LinePragma linePragma)
    {
        LinePragmas.Add(linePragma);
    }

    public void Dispose()
    {
        _sourceMappingsBuilder.Dispose();
        CodeWriter.Dispose();
    }

    private struct ScopeInternal
    {
        public ScopeInternal(IntermediateNodeWriter writer)
        {
            Writer = writer;
        }

        public IntermediateNodeWriter Writer { get; }
    }
}
