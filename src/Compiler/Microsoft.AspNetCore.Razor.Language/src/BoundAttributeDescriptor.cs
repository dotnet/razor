// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Razor.Language.Components;

namespace Microsoft.AspNetCore.Razor.Language;

/// <summary>
/// A metadata class describing a tag helper attribute.
/// </summary>
public abstract class BoundAttributeDescriptor : IEquatable<BoundAttributeDescriptor>
{
    private enum Flags : ushort
    {
        ContainsDiagnostics = 0x01,
        IsDirectiveAttributeComputed = 0x02,
        IsDirectiveAttribute = 0x04,
        IsIndexerStringProperty = 0x08,
        IsIndexerBooleanProperty = 0x10,
        IsEnum = 0x20,
        IsStringProperty = 0x40,
        IsBooleanProperty = 0x80,
        IsEditorRequired = 0x100,
        HasIndexer = 0x200,
        CaseSensitive = 0x400
    }

    private Flags _flags;

    private bool HasFlag(Flags flag) => (_flags & flag) != 0;
    private void SetFlag(Flags flags) => _flags |= flags;
    private void ClearFlag(Flags flags) => _flags &= ~flags;

    private void SetOrClearFlag(Flags flag, bool value)
    {
        if (value)
        {
            SetFlag(flag);
        }
        else
        {
            ClearFlag(flag);
        }
    }

    protected BoundAttributeDescriptor(string kind)
    {
        Kind = kind;
    }

    public string Kind { get; }

    public bool IsIndexerStringProperty
    {
        get => HasFlag(Flags.IsIndexerStringProperty);
        protected set => SetOrClearFlag(Flags.IsIndexerStringProperty, value);
    }

    public bool IsIndexerBooleanProperty
    {
        get => HasFlag(Flags.IsIndexerBooleanProperty);
        protected set => SetOrClearFlag(Flags.IsIndexerBooleanProperty, value);
    }

    public bool IsEnum
    {
        get => HasFlag(Flags.IsEnum);
        protected set => SetOrClearFlag(Flags.IsEnum, value);
    }

    public bool IsStringProperty
    {
        get => HasFlag(Flags.IsStringProperty);
        protected set => SetOrClearFlag(Flags.IsStringProperty, value);
    }

    public bool IsBooleanProperty
    {
        get => HasFlag(Flags.IsBooleanProperty);
        protected set => SetOrClearFlag(Flags.IsBooleanProperty, value);
    }

    internal bool IsEditorRequired
    {
        get => HasFlag(Flags.IsEditorRequired);
        set => SetOrClearFlag(Flags.IsEditorRequired, value);
    }

    public string Name { get; protected set; }

    public string IndexerNamePrefix { get; protected set; }

    public string TypeName { get; protected set; }

    public string IndexerTypeName { get; protected set; }

    public bool HasIndexer
    {
        get => HasFlag(Flags.HasIndexer);
        protected set => SetOrClearFlag(Flags.HasIndexer, value);
    }

    public string Documentation { get; protected set; }

    public string DisplayName { get; protected set; }

    public bool CaseSensitive
    {
        get => HasFlag(Flags.CaseSensitive);
        protected set => SetOrClearFlag(Flags.CaseSensitive, value);
    }

    public bool IsDirectiveAttribute
    {
        get
        {
            if (!HasFlag(Flags.IsDirectiveAttributeComputed))
            {
                // If we haven't computed this value yet, compute it by checking the metadata.
                var isDirectiveAttribute = Metadata.TryGetValue(ComponentMetadata.Common.DirectiveAttribute, out var value) && value == bool.TrueString;
                SetOrClearFlag(Flags.IsDirectiveAttribute, isDirectiveAttribute);
                SetFlag(Flags.IsDirectiveAttributeComputed);
            }

            return HasFlag(Flags.IsDirectiveAttribute);
        }
    }

    public IReadOnlyList<RazorDiagnostic> Diagnostics
    {
        get => HasFlag(Flags.ContainsDiagnostics)
            ? TagHelperDiagnostics.GetDiagnostics(this)
            : Array.Empty<RazorDiagnostic>();

        protected set
        {
            if (value?.Count > 0)
            {
                TagHelperDiagnostics.AddDiagnostics(this, value);
                SetFlag(Flags.ContainsDiagnostics);
            }
            else
            {
                ClearFlag(Flags.ContainsDiagnostics);
            }
        }
    }

    public IReadOnlyDictionary<string, string> Metadata { get; protected set; }

    public virtual IReadOnlyList<BoundAttributeParameterDescriptor> BoundAttributeParameters { get; protected set; }

    public bool HasErrors
    {
        get
        {
            if (!HasFlag(Flags.ContainsDiagnostics))
            {
                return false;
            }

            var errors = Diagnostics.Any(diagnostic => diagnostic.Severity == RazorDiagnosticSeverity.Error);

            return errors;
        }
    }

    public override string ToString()
    {
        return DisplayName ?? base.ToString();
    }

    public bool Equals(BoundAttributeDescriptor other)
    {
        return BoundAttributeDescriptorComparer.Default.Equals(this, other);
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as BoundAttributeDescriptor);
    }

    public override int GetHashCode()
    {
        return BoundAttributeDescriptorComparer.Default.GetHashCode(this);
    }
}
