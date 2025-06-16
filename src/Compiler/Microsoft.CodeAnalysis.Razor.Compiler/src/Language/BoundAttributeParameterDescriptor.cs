// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.AspNetCore.Razor.Utilities;

namespace Microsoft.AspNetCore.Razor.Language;

public sealed class BoundAttributeParameterDescriptor : TagHelperObject<BoundAttributeParameterDescriptor>
{
    private readonly BoundAttributeParameterFlags _flags;
    private readonly DocumentationObject _documentationObject;

    private BoundAttributeDescriptor? _parent;

    public BoundAttributeParameterFlags Flags => _flags;
    public string Name { get; }
    public string PropertyName { get; }
    public string TypeName { get; }
    public string DisplayName { get; }

    public bool CaseSensitive => _flags.IsFlagSet(BoundAttributeParameterFlags.CaseSensitive);
    public bool IsEnum => _flags.IsFlagSet(BoundAttributeParameterFlags.IsEnum);
    public bool IsStringProperty => _flags.IsFlagSet(BoundAttributeParameterFlags.IsStringProperty);
    public bool IsBooleanProperty => _flags.IsFlagSet(BoundAttributeParameterFlags.IsBooleanProperty);
    public bool BindAttributeGetSet => _flags.IsFlagSet(BoundAttributeParameterFlags.BindAttributeGetSet);

    public MetadataCollection Metadata { get; }

    internal BoundAttributeParameterDescriptor(
        BoundAttributeParameterFlags flags,
        string name,
        string propertyName,
        string typeName,
        DocumentationObject documentationObject,
        string displayName,
        MetadataCollection metadata,
        ImmutableArray<RazorDiagnostic> diagnostics)
        : base(diagnostics)
    {
        _flags = flags;

        Name = name;
        PropertyName = propertyName;
        TypeName = typeName;
        _documentationObject = documentationObject;
        DisplayName = displayName;
        Metadata = metadata ?? MetadataCollection.Empty;
    }

    private protected override void BuildChecksum(in Checksum.Builder builder)
    {
        builder.AppendData((byte)Flags);
        builder.AppendData(Name);
        builder.AppendData(PropertyName);
        builder.AppendData(TypeName);
        builder.AppendData(DisplayName);

        DocumentationObject.AppendToChecksum(in builder);

        builder.AppendData(Metadata.Checksum);
    }

    public BoundAttributeDescriptor Parent
        => _parent ?? ThrowHelper.ThrowInvalidOperationException<BoundAttributeDescriptor>(Resources.Parent_has_not_been_set);

    internal void SetParent(BoundAttributeDescriptor parent)
    {
        Debug.Assert(parent != null);
        Debug.Assert(_parent == null);

        _parent = parent;
    }

    public string? Documentation => _documentationObject.GetText();

    internal DocumentationObject DocumentationObject => _documentationObject;

    public override string ToString()
        => DisplayName ?? base.ToString()!;
}
