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
    private readonly record struct ScopeInternal(IntermediateNodeWriter Writer);

    internal static readonly object NewLineString = "NewLineString";

    internal static readonly object SuppressUniqueIds = "SuppressUniqueIds";

    public RazorCodeGenerationOptions Options { get; }
    public CodeWriter CodeWriter { get; }
    public RazorDiagnosticCollection Diagnostics { get; }
    public ItemCollection Items { get; }

    private readonly Stack<IntermediateNode> _ancestors;
    private readonly RazorCodeDocument _codeDocument;
    private readonly DocumentIntermediateNode _documentNode;

    private readonly PooledObject<Stack<ScopeInternal>> _pooledScopeStack;
    private readonly PooledObject<ImmutableArray<SourceMapping>.Builder> _pooledSourceMappings;
    private readonly PooledObject<List<LinePragma>> _pooledLinePragmas;

    public CodeRenderingContext(
        IntermediateNodeWriter nodeWriter,
        RazorCodeDocument codeDocument,
        DocumentIntermediateNode documentNode,
        RazorCodeGenerationOptions options)
    {
        ArgHelper.ThrowIfNull(nodeWriter);
        ArgHelper.ThrowIfNull(codeDocument);
        ArgHelper.ThrowIfNull(documentNode);
        ArgHelper.ThrowIfNull(options);

        _codeDocument = codeDocument;
        _documentNode = documentNode;
        Options = options;

        _ancestors = new Stack<IntermediateNode>();
        Diagnostics = [];
        Items = [];
        _pooledSourceMappings = ArrayBuilderPool<SourceMapping>.GetPooledObject();
        _pooledLinePragmas = ListPool<LinePragma>.GetPooledObject();

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

        _pooledScopeStack = StackPool<ScopeInternal>.GetPooledObject(out var scopeStack);
        scopeStack.Push(new(nodeWriter));
    }

    // This will be initialized by the document writer when the context is 'live'.
    public IntermediateNodeVisitor Visitor { get; set; }

    public IEnumerable<IntermediateNode> Ancestors => _ancestors;

    internal Stack<IntermediateNode> AncestorsInternal => _ancestors;

    public string DocumentKind => _documentNode.DocumentKind;

    public ImmutableArray<SourceMapping>.Builder SourceMappings => _pooledSourceMappings.Object;

    internal List<LinePragma> LinePragmas => _pooledLinePragmas.Object;

    public IntermediateNodeWriter NodeWriter => Current.Writer;

    public IntermediateNode Parent => _ancestors.Count == 0 ? null : _ancestors.Peek();

    public RazorSourceDocument SourceDocument => _codeDocument.Source;

    private Stack<ScopeInternal> ScopeStack => _pooledScopeStack.Object;
    private ScopeInternal Current => ScopeStack.Peek();

    public void AddSourceMappingFor(IntermediateNode node)
    {
        ArgHelper.ThrowIfNull(node);

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
        ArgHelper.ThrowIfNull(node);

        _ancestors.Push(node);

        for (var i = 0; i < node.Children.Count; i++)
        {
            Visitor.Visit(node.Children[i]);
        }

        _ancestors.Pop();
    }

    public void RenderChildren(IntermediateNode node, IntermediateNodeWriter writer)
    {
        ArgHelper.ThrowIfNull(node);
        ArgHelper.ThrowIfNull(writer);

        ScopeStack.Push(new ScopeInternal(writer));
        _ancestors.Push(node);

        for (var i = 0; i < node.Children.Count; i++)
        {
            Visitor.Visit(node.Children[i]);
        }

        _ancestors.Pop();
        ScopeStack.Pop();
    }

    public void RenderNode(IntermediateNode node)
    {
        ArgHelper.ThrowIfNull(node);

        Visitor.Visit(node);
    }

    public void RenderNode(IntermediateNode node, IntermediateNodeWriter writer)
    {
        ArgHelper.ThrowIfNull(node);
        ArgHelper.ThrowIfNull(writer);

        ScopeStack.Push(new ScopeInternal(writer));

        Visitor.Visit(node);

        ScopeStack.Pop();
    }

    public void AddLinePragma(LinePragma linePragma)
    {
        LinePragmas.Add(linePragma);
    }

    public void Dispose()
    {
        _pooledLinePragmas.Dispose();
        _pooledScopeStack.Dispose();
        _pooledSourceMappings.Dispose();
        CodeWriter.Dispose();
    }
}
