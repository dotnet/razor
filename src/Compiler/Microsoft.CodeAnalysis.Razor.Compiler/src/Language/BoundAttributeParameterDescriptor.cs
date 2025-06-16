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
    private readonly TypeNameObject _typeNameObject;

    private BoundAttributeDescriptor? _parent;
    private string? _displayName;

    public BoundAttributeParameterFlags Flags => _flags;
    public string Name { get; }
    public string PropertyName { get; }
    public string TypeName => _typeNameObject.GetTypeName().AssumeNotNull();
    public string DisplayName => _displayName ??= ":" + Name;

    public string? Documentation => _documentationObject.GetText();

    public bool CaseSensitive => _flags.IsFlagSet(BoundAttributeParameterFlags.CaseSensitive);
    public bool IsEnum => _flags.IsFlagSet(BoundAttributeParameterFlags.IsEnum);
    public bool IsStringProperty => _typeNameObject.IsString;
    public bool IsBooleanProperty => _typeNameObject.IsBoolean;
    public bool BindAttributeGetSet => _flags.IsFlagSet(BoundAttributeParameterFlags.BindAttributeGetSet);

    internal TypeNameObject TypeNameObject => _typeNameObject;
    internal DocumentationObject DocumentationObject => _documentationObject;

    internal BoundAttributeParameterDescriptor(
        BoundAttributeParameterFlags flags,
        string name,
        string propertyName,
        TypeNameObject typeNameObject,
        DocumentationObject documentationObject,
        ImmutableArray<RazorDiagnostic> diagnostics)
        : base(diagnostics)
    {
        _flags = flags;

        Name = name;
        PropertyName = propertyName;
        _typeNameObject = typeNameObject;
        _documentationObject = documentationObject;
    }

    private protected override void BuildChecksum(in Checksum.Builder builder)
    {
        builder.AppendData((byte)Flags);
        builder.AppendData(Name);
        builder.AppendData(PropertyName);

        TypeNameObject.AppendToChecksum(in builder);
        DocumentationObject.AppendToChecksum(in builder);
    }

    public BoundAttributeDescriptor Parent
        => _parent ?? ThrowHelper.ThrowInvalidOperationException<BoundAttributeDescriptor>(Resources.Parent_has_not_been_set);

    internal void SetParent(BoundAttributeDescriptor parent)
    {
        Debug.Assert(parent != null);
        Debug.Assert(_parent == null);

        _parent = parent;
    }

    public override string ToString()
        => DisplayName;
}
