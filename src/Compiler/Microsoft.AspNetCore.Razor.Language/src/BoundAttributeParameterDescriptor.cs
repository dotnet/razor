// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class BoundAttributeParameterDescriptor : TagHelperObject, IEquatable<BoundAttributeParameterDescriptor>
{
    private const int IsEnumBit = LastFlagBit << 1;
    private const int IsStringPropertyBit = LastFlagBit << 2;
    private const int IsBooleanPropertyBit = LastFlagBit << 3;

    private readonly DocumentationObject _documentationObject;

    public string Kind { get; }
    public string Name { get; }
    public string TypeName { get; }
    public string DisplayName { get; }

    public bool IsEnum => HasFlag(IsEnumBit);
    public bool IsStringProperty => HasFlag(IsStringPropertyBit);
    public bool IsBooleanProperty => HasFlag(IsBooleanPropertyBit);

    public bool CaseSensitive => HasFlag(CaseSensitiveBit);

    public MetadataCollection Metadata { get; }

    internal BoundAttributeParameterDescriptor(
        string kind,
        string name,
        string typeName,
        bool isEnum,
        DocumentationObject documentationObject,
        string displayName,
        bool caseSensitive,
        MetadataCollection metadata,
        ImmutableArray<RazorDiagnostic> diagnostics)
    {
        Kind = kind;
        Name = name;
        TypeName = typeName;

        SetOrClearFlag(IsEnumBit, isEnum);
        SetOrClearFlag(CaseSensitiveBit, caseSensitive);

        var isStringProperty = typeName == typeof(string).FullName || typeName == "string";
        SetOrClearFlag(IsStringPropertyBit, isStringProperty);

        var isBooleanProperty = typeName == typeof(bool).FullName || typeName == "bool";
        SetOrClearFlag(IsBooleanPropertyBit, isBooleanProperty);

        _documentationObject = documentationObject;
        DisplayName = displayName;

        Metadata = metadata;

        if (!diagnostics.IsDefaultOrEmpty)
        {
            SetFlag(ContainsDiagnosticsBit);
            TagHelperDiagnostics.AddDiagnostics(this, diagnostics);
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
        => DisplayName ?? base.ToString();

    public bool Equals(BoundAttributeParameterDescriptor other)
        => BoundAttributeParameterDescriptorComparer.Default.Equals(this, other);

    public override bool Equals(object? obj)
        => obj is BoundAttributeParameterDescriptor other &&
           Equals(other);

    public override int GetHashCode()
        => BoundAttributeParameterDescriptorComparer.Default.GetHashCode(this);
}
