// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Components;

namespace Microsoft.AspNetCore.Razor.Language;

/// <summary>
/// A metadata class describing a tag helper attribute.
/// </summary>
public class BoundAttributeDescriptor : TagHelperObject, IEquatable<BoundAttributeDescriptor>
{
    private const int IsDirectiveAttributeComputedBit = 1 << 1;
    private const int IsDirectiveAttributeBit = 1 << 2;
    private const int IsIndexerStringPropertyBit = 1 << 3;
    private const int IsIndexerBooleanPropertyBit = 1 << 4;
    private const int IsEnumBit = 1 << 5;
    private const int IsStringPropertyBit = 1 << 6;
    private const int IsBooleanPropertyBit = 1 << 7;
    private const int IsEditorRequiredBit = 1 << 8;
    private const int HasIndexerBit = 1 << 9;
    private const int CaseSensitiveBit = 1 << 10;

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

    public MetadataCollection Metadata { get; }
    public ImmutableArray<BoundAttributeParameterDescriptor> Parameters { get; }

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

        if (!diagnostics.IsDefaultOrEmpty)
        {
            SetFlag(ContainsDiagnosticsBit);
            TagHelperDiagnostics.AddDiagnostics(this, diagnostics);
        }
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

    public ImmutableArray<RazorDiagnostic> Diagnostics
        => HasFlag(ContainsDiagnosticsBit)
            ? TagHelperDiagnostics.GetDiagnostics(this)
            : ImmutableArray<RazorDiagnostic>.Empty;

    public bool HasErrors
        => HasFlag(ContainsDiagnosticsBit) &&
           Diagnostics.Any(static d => d.Severity == RazorDiagnosticSeverity.Error);

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
