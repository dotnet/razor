﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Razor.Language;

internal sealed class DefaultBoundAttributeDescriptor : BoundAttributeDescriptor
{
    public DefaultBoundAttributeDescriptor(
        string kind,
        string? name,
        string? typeName,
        bool isEnum,
        bool hasIndexer,
        string? indexerNamePrefix,
        string? indexerTypeName,
        DocumentationObject documentationObject,
        string? displayName,
        bool caseSensitive,
        bool isEditorRequired,
        BoundAttributeParameterDescriptor[] parameterDescriptors,
        MetadataCollection metadata,
        RazorDiagnostic[] diagnostics)
        : base(kind)
    {
        Name = name;
        TypeName = typeName;
        IsEnum = isEnum;
        HasIndexer = hasIndexer;
        IndexerNamePrefix = indexerNamePrefix;
        IndexerTypeName = indexerTypeName;
        DocumentationObject = documentationObject;
        DisplayName = displayName;
        CaseSensitive = caseSensitive;
        IsEditorRequired = isEditorRequired;
        BoundAttributeParameters = parameterDescriptors;

        Metadata = metadata;
        Diagnostics = diagnostics;

        IsIndexerStringProperty = indexerTypeName == typeof(string).FullName || indexerTypeName == "string";
        IsStringProperty = typeName == typeof(string).FullName || typeName == "string";

        IsIndexerBooleanProperty = indexerTypeName == typeof(bool).FullName || indexerTypeName == "bool";
        IsBooleanProperty = typeName == typeof(bool).FullName || typeName == "bool";
    }
}
