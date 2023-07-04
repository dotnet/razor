﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language.Intermediate;

namespace Microsoft.AspNetCore.Razor.Language.CodeGeneration;

internal class DefaultCodeRenderingContext : CodeRenderingContext
{
    private readonly Stack<IntermediateNode> _ancestors;
    private readonly RazorCodeDocument _codeDocument;
    private readonly DocumentIntermediateNode _documentNode;
    private readonly List<ScopeInternal> _scopes;

    public DefaultCodeRenderingContext(
        CodeWriter codeWriter,
        IntermediateNodeWriter nodeWriter,
        RazorCodeDocument codeDocument,
        DocumentIntermediateNode documentNode,
        RazorCodeGenerationOptions options)
    {
        if (codeWriter == null)
        {
            throw new ArgumentNullException(nameof(codeWriter));
        }

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

        CodeWriter = codeWriter;
        _codeDocument = codeDocument;
        _documentNode = documentNode;
        Options = options;

        _ancestors = new Stack<IntermediateNode>();
        Diagnostics = new RazorDiagnosticCollection();
        Items = new ItemCollection();
        SourceMappings = new List<SourceMapping>();
        LinePragmas = new List<LinePragma>();

        var diagnostics = _documentNode.GetAllDiagnostics();
        for (var i = 0; i < diagnostics.Count; i++)
        {
            Diagnostics.Add(diagnostics[i]);
        }

        var newLineString = codeDocument.Items[NewLineString];
        if (newLineString != null)
        {
            // Set new line character to a specific string regardless of platform, for testing purposes.
            codeWriter.NewLine = (string)newLineString;
        }

        Items[NewLineString] = codeDocument.Items[NewLineString];
        Items[SuppressUniqueIds] = codeDocument.Items[SuppressUniqueIds] ?? options.SuppressUniqueIds;

        _scopes = new List<ScopeInternal>();
        _scopes.Add(new ScopeInternal(nodeWriter));
    }

    // This will be initialized by the document writer when the context is 'live'.
    public IntermediateNodeVisitor Visitor { get; set; }

    public override IEnumerable<IntermediateNode> Ancestors => _ancestors;

    internal Stack<IntermediateNode> AncestorsInternal => _ancestors;

    public override CodeWriter CodeWriter { get; }

    public override RazorDiagnosticCollection Diagnostics { get; }

    public override string DocumentKind => _documentNode.DocumentKind;

    public override ItemCollection Items { get; }

    public List<SourceMapping> SourceMappings { get; }

    internal List<LinePragma> LinePragmas { get; }

    public override IntermediateNodeWriter NodeWriter => Current.Writer;

    public override RazorCodeGenerationOptions Options { get; }

    public override IntermediateNode Parent => _ancestors.Count == 0 ? null : _ancestors.Peek();

    public override RazorSourceDocument SourceDocument => _codeDocument.Source;

    private ScopeInternal Current => _scopes[_scopes.Count - 1];

    public override void AddSourceMappingFor(IntermediateNode node)
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

    public override void AddSourceMappingFor(SourceSpan source)
    {
        if (SourceDocument.FilePath != null &&
            !string.Equals(SourceDocument.FilePath, source.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            // We don't want to generate line mappings for imports.
            return;
        }

        var generatedLocation = new SourceSpan(CodeWriter.Location, source.Length);
        var sourceMapping = new SourceMapping(source, generatedLocation);

        SourceMappings.Add(sourceMapping);
    }

    public override void RenderChildren(IntermediateNode node)
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

    public override void RenderChildren(IntermediateNode node, IntermediateNodeWriter writer)
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

    public override void RenderNode(IntermediateNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        Visitor.Visit(node);
    }

    public override void RenderNode(IntermediateNode node, IntermediateNodeWriter writer)
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

    public override void AddLinePragma(LinePragma linePragma)
    {
        LinePragmas.Add(linePragma);
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
