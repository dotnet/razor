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
    public ItemCollection Items { get; }

    private readonly RazorCodeDocument _codeDocument;
    private readonly DocumentIntermediateNode _documentNode;

    private readonly Stack<IntermediateNode> _ancestorStack;
    private readonly Stack<ScopeInternal> _scopeStack;

    private readonly ImmutableArray<RazorDiagnostic>.Builder _diagnostics;
    private readonly ImmutableArray<SourceMapping>.Builder _sourceMappings;
    private readonly ImmutableArray<LinePragma>.Builder _linePragmas;

    public string DocumentKind => _documentNode.DocumentKind;
    public RazorSourceDocument SourceDocument => _codeDocument.Source;

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

        _ancestorStack = StackPool<IntermediateNode>.Default.Get();
        _scopeStack = StackPool<ScopeInternal>.Default.Get();
        _scopeStack.Push(new(nodeWriter));

        _diagnostics = ArrayBuilderPool<RazorDiagnostic>.Default.Get();

        foreach (var diagnostic in _documentNode.GetAllDiagnostics().AsEnumerable())
        {
            _diagnostics.Add(diagnostic);
        }

        _linePragmas = ArrayBuilderPool<LinePragma>.Default.Get();
        _sourceMappings = ArrayBuilderPool<SourceMapping>.Default.Get();

        Items = [];

        // Set new line character to a specific string regardless of platform, for testing purposes.
        var newLineString = codeDocument.Items[NewLineString] as string ?? Environment.NewLine;
        CodeWriter = new CodeWriter(newLineString, options);

        Items[NewLineString] = codeDocument.Items[NewLineString];
        Items[SuppressUniqueIds] = codeDocument.Items[SuppressUniqueIds] ?? options.SuppressUniqueIds;
    }

    public void Dispose()
    {
        StackPool<IntermediateNode>.Default.Return(_ancestorStack);
        StackPool<ScopeInternal>.Default.Return(_scopeStack);

        ArrayBuilderPool<RazorDiagnostic>.Default.Return(_diagnostics);
        ArrayBuilderPool<LinePragma>.Default.Return(_linePragmas);
        ArrayBuilderPool<SourceMapping>.Default.Return(_sourceMappings);

        CodeWriter.Dispose();
    }

    // This will be initialized by the document writer when the context is 'live'.
    public IntermediateNodeVisitor Visitor { get; set; }

    public IntermediateNodeWriter NodeWriter => _scopeStack.Peek().Writer;

    public IntermediateNode Parent => _ancestorStack.Count == 0 ? null : _ancestorStack.Peek();

    public void AddDiagnostic(RazorDiagnostic diagnostic)
    {
        _diagnostics.Add(diagnostic);
    }

    public ImmutableArray<RazorDiagnostic> GetDiagnostics()
        => _diagnostics.ToImmutableOrderedBy(static d => d.Span.AbsoluteIndex);

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

        var currentLocation = CodeWriter.Location with
        {
            AbsoluteIndex = CodeWriter.Location.AbsoluteIndex + offset,
            CharacterIndex = CodeWriter.Location.CharacterIndex + offset
        };

        var generatedLocation = new SourceSpan(currentLocation, source.Length);
        var sourceMapping = new SourceMapping(source, generatedLocation);

        _sourceMappings.Add(sourceMapping);
    }

    public ImmutableArray<SourceMapping> GetSourceMappings()
        => _sourceMappings.DrainToImmutable();

    public void RenderChildren(IntermediateNode node)
    {
        ArgHelper.ThrowIfNull(node);

        _ancestorStack.Push(node);

        for (var i = 0; i < node.Children.Count; i++)
        {
            Visitor.Visit(node.Children[i]);
        }

        _ancestorStack.Pop();
    }

    public void RenderChildren(IntermediateNode node, IntermediateNodeWriter writer)
    {
        ArgHelper.ThrowIfNull(node);
        ArgHelper.ThrowIfNull(writer);

        _scopeStack.Push(new ScopeInternal(writer));
        _ancestorStack.Push(node);

        for (var i = 0; i < node.Children.Count; i++)
        {
            Visitor.Visit(node.Children[i]);
        }

        _ancestorStack.Pop();
        _scopeStack.Pop();
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

        _scopeStack.Push(new ScopeInternal(writer));

        Visitor.Visit(node);

        _scopeStack.Pop();
    }

    public void AddLinePragma(LinePragma linePragma)
    {
        _linePragmas.Add(linePragma);
    }

    public ImmutableArray<LinePragma> GetLinePragmas()
        => _linePragmas.DrainToImmutable();

    public void PushAncestor(IntermediateNode node)
    {
        _ancestorStack.Push(node);
    }
}
