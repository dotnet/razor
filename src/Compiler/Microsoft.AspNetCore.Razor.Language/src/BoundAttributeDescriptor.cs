﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language.Components;

namespace Microsoft.AspNetCore.Razor.Language;

/// <summary>
/// A metadata class describing a tag helper attribute.
/// </summary>
public sealed class BoundAttributeDescriptor : TagHelperObject, IEquatable<BoundAttributeDescriptor>
{
    [Flags]
    private enum BoundAttributeFlags
    {
        CaseSensitive = 1 << 0,
        HasIndexer = 1 << 1,
        IsIndexerStringProperty = 1 << 2,
        IsIndexerBooleanProperty = 1 << 3,
        IsEnum = 1 << 4,
        IsStringProperty = 1 << 5,
        IsBooleanProperty = 1 << 6,
        IsEditorRequired = 1 << 7,
        IsDirectiveAttribute = 1 << 8
    }

    private readonly BoundAttributeFlags _flags;
    private readonly DocumentationObject _documentationObject;

    public string Kind { get; }
    public string Name { get; }
    public string TypeName { get; }
    public string DisplayName { get; }

    public string? IndexerNamePrefix { get; }
    public string? IndexerTypeName { get; }

    public bool CaseSensitive => (_flags & BoundAttributeFlags.CaseSensitive) != 0;
    public bool HasIndexer => (_flags & BoundAttributeFlags.HasIndexer) != 0;
    public bool IsIndexerStringProperty => (_flags & BoundAttributeFlags.IsIndexerStringProperty) != 0;
    public bool IsIndexerBooleanProperty => (_flags & BoundAttributeFlags.IsIndexerBooleanProperty) != 0;
    public bool IsEnum => (_flags & BoundAttributeFlags.IsEnum) != 0;
    public bool IsStringProperty => (_flags & BoundAttributeFlags.IsStringProperty) != 0;
    public bool IsBooleanProperty => (_flags & BoundAttributeFlags.IsBooleanProperty) != 0;
    internal bool IsEditorRequired => (_flags & BoundAttributeFlags.IsEditorRequired) != 0;
    public bool IsDirectiveAttribute => (_flags & BoundAttributeFlags.IsDirectiveAttribute) != 0;

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
        _documentationObject = documentationObject;
        DisplayName = displayName;
        Parameters = parameters.NullToEmpty();
        Metadata = metadata ?? MetadataCollection.Empty;

        BoundAttributeFlags flags = 0;

        if (isEnum)
        {
            flags |= BoundAttributeFlags.IsEnum;
        }

        if (hasIndexer)
        {
            flags |= BoundAttributeFlags.HasIndexer;
        }

        if (caseSensitive)
        {
            flags |= BoundAttributeFlags.CaseSensitive;
        }

        if (isEditorRequired)
        {
            flags |= BoundAttributeFlags.IsEditorRequired;
        }

        if (indexerTypeName == typeof(string).FullName || indexerTypeName == "string")
        {
            flags |= BoundAttributeFlags.IsIndexerStringProperty;
        }

        if (indexerTypeName == typeof(bool).FullName || indexerTypeName == "bool")
        {
            flags |= BoundAttributeFlags.IsIndexerBooleanProperty;
        }

        if (typeName == typeof(string).FullName || typeName == "string")
        {
            flags |= BoundAttributeFlags.IsStringProperty;
        }

        if (typeName == typeof(bool).FullName || typeName == "bool")
        {
            flags |= BoundAttributeFlags.IsBooleanProperty;
        }

        if (Metadata.Contains(ComponentMetadata.Common.DirectiveAttribute, bool.TrueString))
        {
            flags |= BoundAttributeFlags.IsDirectiveAttribute;
        }

        _flags = flags;
    }

    public string? Documentation => _documentationObject.GetText();

    internal DocumentationObject DocumentationObject => _documentationObject;

    public IEnumerable<RazorDiagnostic> GetAllDiagnostics()
    {
        foreach (var parameter in Parameters)
        {
            foreach (var diagnostic in parameter.Diagnostics)
            {
                yield return diagnostic;
            }
        }

        foreach (var diagnostic in Diagnostics)
        {
            yield return diagnostic;
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
