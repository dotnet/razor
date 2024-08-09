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

    private readonly PooledObject<Stack<IntermediateNode>> _pooledAncestors;
    private readonly PooledObject<ImmutableArray<RazorDiagnostic>.Builder> _pooledDiagnostics;
    private readonly PooledObject<Stack<ScopeInternal>> _pooledScopeStack;
    private readonly PooledObject<ImmutableArray<SourceMapping>.Builder> _pooledSourceMappings;
    private readonly PooledObject<ImmutableArray<LinePragma>.Builder> _pooledLinePragmas;

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

        _pooledAncestors = StackPool<IntermediateNode>.GetPooledObject();
        _pooledDiagnostics = ArrayBuilderPool<RazorDiagnostic>.GetPooledObject();
        Items = [];
        _pooledSourceMappings = ArrayBuilderPool<SourceMapping>.GetPooledObject();
        _pooledLinePragmas = ArrayBuilderPool<LinePragma>.GetPooledObject();

        foreach (var diagnostic in _documentNode.GetAllDiagnostics().AsEnumerable())
        {
            Diagnostics.Add(diagnostic);
        }

        // Set new line character to a specific string regardless of platform, for testing purposes.
        var newLineString = codeDocument.Items[NewLineString] as string ?? Environment.NewLine;
        CodeWriter = new CodeWriter(newLineString, options);

        Items[NewLineString] = codeDocument.Items[NewLineString];
        Items[SuppressUniqueIds] = codeDocument.Items[SuppressUniqueIds] ?? options.SuppressUniqueIds;

        _pooledScopeStack = StackPool<ScopeInternal>.GetPooledObject(out var scopeStack);
        scopeStack.Push(new(nodeWriter));
    }

    public void Dispose()
    {
        _pooledAncestors.Dispose();
        _pooledDiagnostics.Dispose();
        _pooledLinePragmas.Dispose();
        _pooledScopeStack.Dispose();
        _pooledSourceMappings.Dispose();
        CodeWriter.Dispose();
    }

    // This will be initialized by the document writer when the context is 'live'.
    public IntermediateNodeVisitor Visitor { get; set; }

    public IEnumerable<IntermediateNode> Ancestors => _pooledAncestors.Object;

    internal Stack<IntermediateNode> AncestorsInternal => _pooledAncestors.Object;

    public ImmutableArray<RazorDiagnostic>.Builder Diagnostics => _pooledDiagnostics.Object;

    public string DocumentKind => _documentNode.DocumentKind;

    public ImmutableArray<SourceMapping>.Builder SourceMappings => _pooledSourceMappings.Object;

    internal ImmutableArray<LinePragma>.Builder LinePragmas => _pooledLinePragmas.Object;

    public IntermediateNodeWriter NodeWriter => Current.Writer;

    public IntermediateNode Parent => AncestorsInternal.Count == 0 ? null : AncestorsInternal.Peek();

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

        AncestorsInternal.Push(node);

        for (var i = 0; i < node.Children.Count; i++)
        {
            Visitor.Visit(node.Children[i]);
        }

        AncestorsInternal.Pop();
    }

    public void RenderChildren(IntermediateNode node, IntermediateNodeWriter writer)
    {
        ArgHelper.ThrowIfNull(node);
        ArgHelper.ThrowIfNull(writer);

        ScopeStack.Push(new ScopeInternal(writer));
        AncestorsInternal.Push(node);

        for (var i = 0; i < node.Children.Count; i++)
        {
            Visitor.Visit(node.Children[i]);
        }

        AncestorsInternal.Pop();
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
}
