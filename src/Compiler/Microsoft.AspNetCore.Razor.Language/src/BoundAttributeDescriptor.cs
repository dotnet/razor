// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

/// <summary>
/// A metadata class describing a tag helper attribute.
/// </summary>
public sealed class BoundAttributeDescriptor : TagHelperObject, IEquatable<BoundAttributeDescriptor>
{
    private const int IsDirectiveAttributeComputedBit = LastFlagBit << 1;
    private const int IsDirectiveAttributeBit = LastFlagBit << 2;
    private const int IsIndexerStringPropertyBit = LastFlagBit << 3;
    private const int IsIndexerBooleanPropertyBit = LastFlagBit << 4;
    private const int IsEnumBit = LastFlagBit << 5;
    private const int IsStringPropertyBit = LastFlagBit << 6;
    private const int IsBooleanPropertyBit = LastFlagBit << 7;
    private const int IsEditorRequiredBit = LastFlagBit << 8;
    private const int HasIndexerBit = LastFlagBit << 9;

    private ImmutableArray<RazorDiagnostic>? _allDiagnostics;
    private readonly DocumentationObject _documentationObject;

    public string Kind { get; }
    public string Name { get; }
    public string TypeName { get; }
    public string DisplayName { get; }

    public string? IndexerNamePrefix { get; }
    public string? IndexerTypeName { get; }

    public bool IsIndexerStringProperty => HasFlag(IsIndexerStringPropertyBit);
    public bool IsIndexerBooleanProperty => HasFlag(IsIndexerBooleanPropertyBit);
    public bool IsEnum => HasFlag(IsEnumBit);
    public bool IsStringProperty => HasFlag(IsStringPropertyBit);
    public bool IsBooleanProperty => HasFlag(IsBooleanPropertyBit);
    internal bool IsEditorRequired => HasFlag(IsEditorRequiredBit);

    public bool HasIndexer => HasFlag(HasIndexerBit);

    public bool CaseSensitive => HasFlag(CaseSensitiveBit);

    public ImmutableArray<BoundAttributeParameterDescriptor> Parameters { get; }
    public MetadataCollection Metadata { get; }

    internal BoundAttributeDescriptor(
        string kind,
        string name,
        string typeName,
        bool isEnum,
        bool hasIndexer,
        string? indexerNamePrefix,
        string? indexerTypeName,
        DocumentationObject documentationObject,
        string displayName,
        bool caseSensitive,
        bool isEditorRequired,
        ImmutableArray<BoundAttributeParameterDescriptor> parameters,
        MetadataCollection metadata,
        ImmutableArray<RazorDiagnostic> diagnostics)
        : base(diagnostics)
    {
        Kind = kind;
        Name = name;
        TypeName = typeName;
        IndexerNamePrefix = indexerNamePrefix;
        IndexerTypeName = indexerTypeName;

        SetOrClearFlag(IsEnumBit, isEnum);
        SetOrClearFlag(HasIndexerBit, hasIndexer);
        SetOrClearFlag(CaseSensitiveBit, caseSensitive);
        SetOrClearFlag(IsEditorRequiredBit, isEditorRequired);

        var isIndexerStringProperty = indexerTypeName == typeof(string).FullName || indexerTypeName == "string";
        SetOrClearFlag(IsIndexerStringPropertyBit, isIndexerStringProperty);

        var isStringProperty = typeName == typeof(string).FullName || typeName == "string";
        SetOrClearFlag(IsStringPropertyBit, isStringProperty);

        var isIndexerBooleanProperty = indexerTypeName == typeof(bool).FullName || indexerTypeName == "bool";
        SetOrClearFlag(IsIndexerBooleanPropertyBit, isIndexerBooleanProperty);

        var isBooleanProperty = typeName == typeof(bool).FullName || typeName == "bool";
        SetOrClearFlag(IsBooleanPropertyBit, isBooleanProperty);

        _documentationObject = documentationObject;
        DisplayName = displayName;

        Parameters = parameters.NullToEmpty();
        Metadata = metadata;
    }

    public bool IsDirectiveAttribute
    {
        get
        {
            if (!HasFlag(IsDirectiveAttributeComputedBit))
            {
                // If we haven't computed this value yet, compute it by checking the metadata.
                var isDirectiveAttribute = Metadata.TryGetValue(ComponentMetadata.Common.DirectiveAttribute, out var value) && value == bool.TrueString;
                if (isDirectiveAttribute)
                {
                    SetFlag(IsDirectiveAttributeBit | IsDirectiveAttributeComputedBit);
                }
                else
                {
                    ClearFlag(IsDirectiveAttributeBit);
                    SetFlag(IsDirectiveAttributeComputedBit);
                }
            }

            return HasFlag(IsDirectiveAttributeBit);
        }
    }

    public string? Documentation => _documentationObject.GetText();

    internal DocumentationObject DocumentationObject => _documentationObject;

    public ImmutableArray<RazorDiagnostic> GetAllDiagnostics()
    {
        return _allDiagnostics ??= GetAllDiagnosticsCore();

        ImmutableArray<RazorDiagnostic> GetAllDiagnosticsCore()
        {
            using var diagnostics = new PooledArrayBuilder<RazorDiagnostic>();

            foreach (var parameter in Parameters)
            {
                diagnostics.AddRange(parameter.Diagnostics);
            }

            diagnostics.AddRange(Diagnostics);

            return diagnostics.DrainToImmutable();
        }
    }

    public override string ToString()
    {
        return DisplayName ?? base.ToString();
    }

    public bool Equals(BoundAttributeDescriptor other)
        => BoundAttributeDescriptorComparer.Default.Equals(this, other);

    public override bool Equals(object? obj)
        => obj is BoundAttributeDescriptor other &&
           Equals(other);

    public override int GetHashCode()
        => BoundAttributeDescriptorComparer.Default.GetHashCode(this);
}
