// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.Extensions.ObjectPool;

namespace Microsoft.AspNetCore.Razor.Language;

public partial class BoundAttributeParameterDescriptorBuilder : IBuilder<BoundAttributeParameterDescriptor>
{
    private static readonly ObjectPool<BoundAttributeParameterDescriptorBuilder> s_pool = DefaultPool.Create(Policy.Instance);

    internal static BoundAttributeParameterDescriptorBuilder GetInstance(DefaultBoundAttributeDescriptorBuilder parent, string kind)
    {
        var builder = s_pool.Get();

        builder._parent = parent;
        builder._kind = kind;

        return builder;
    }

    internal static void ReturnInstance(BoundAttributeParameterDescriptorBuilder builder)
        => s_pool.Return(builder);

    [AllowNull]
    private DefaultBoundAttributeDescriptorBuilder _parent;
    [AllowNull]
    private string _kind;
    private DocumentationObject _documentationObject;
    private MetadataHolder _metadata;

    private ImmutableArray<RazorDiagnostic>.Builder? _diagnostics;

    private BoundAttributeParameterDescriptorBuilder()
    {
    }

    internal BoundAttributeParameterDescriptorBuilder(DefaultBoundAttributeDescriptorBuilder parent, string kind)
    {
        _parent = parent;
        _kind = kind;
    }

    public string? Name { get; set; }
    public string? TypeName { get; set; }
    public bool IsEnum { get; set; }

    public string? Documentation
    {
        get => _documentationObject.GetText();
        set => _documentationObject = new(value);
    }

    public string? DisplayName { get; set; }

    public IDictionary<string, string?> Metadata => _metadata.MetadataDictionary;

    public void SetMetadata(MetadataCollection metadata) => _metadata.SetMetadataCollection(metadata);

    public bool TryGetMetadataValue(string key, [NotNullWhen(true)] out string? value)
        => _metadata.TryGetMetadataValue(key, out value);

    public ImmutableArray<RazorDiagnostic>.Builder Diagnostics => _diagnostics ??= ImmutableArray.CreateBuilder<RazorDiagnostic>();

    internal bool CaseSensitive => _parent.CaseSensitive;

    internal void SetDocumentation(string? text)
    {
        _documentationObject = new(text);
    }

    internal void SetDocumentation(DocumentationDescriptor? documentation)
    {
        _documentationObject = new(documentation);
    }

    public BoundAttributeParameterDescriptor Build()
    {
        var diagnostics = new PooledHashSet<RazorDiagnostic>();
        try
        {
            Validate(ref diagnostics);

            diagnostics.UnionWith(_diagnostics);

            var metadata = _metadata.GetMetadataCollection();

            var descriptor = new BoundAttributeParameterDescriptor(
                _kind,
                Name!, // Name is not expected to be null. If it is, a diagnostic will be created for it.
                TypeName.AssumeNotNull(),
                IsEnum,
                _documentationObject,
                GetDisplayName(),
                CaseSensitive,
                metadata,
                diagnostics.ToImmutableArray());

            return descriptor;
        }
        finally
        {
            diagnostics.ClearAndFree();
        }
    }

    private string GetDisplayName()
        => DisplayName ?? $":{Name}";

    private void Validate(ref PooledHashSet<RazorDiagnostic> diagnostics)
    {
        if (Name.IsNullOrWhiteSpace())
        {
            var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidBoundAttributeParameterNullOrWhitespace(_parent.Name);

            diagnostics.Add(diagnostic);
        }
        else
        {
            foreach (var character in Name)
            {
                if (char.IsWhiteSpace(character) || HtmlConventions.IsInvalidNonWhitespaceHtmlCharacters(character))
                {
                    var diagnostic = RazorDiagnosticFactory.CreateTagHelper_InvalidBoundAttributeParameterName(
                        _parent.Name,
                        Name,
                        character);

                    diagnostics.Add(diagnostic);
                }
            }
        }
    }
}
