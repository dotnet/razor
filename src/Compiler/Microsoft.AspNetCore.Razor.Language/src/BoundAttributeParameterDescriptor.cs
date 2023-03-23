// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.Language;

public abstract class BoundAttributeParameterDescriptor : IEquatable<BoundAttributeParameterDescriptor>
{
    private enum Flags : byte
    {
        ContainsDiagnostics = 0x01,
        IsEnum = 0x02,
        IsStringProperty = 0x04,
        IsBooleanProperty = 0x08,
        CaseSensitive = 0x10
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

    protected BoundAttributeParameterDescriptor(string kind)
    {
        Kind = kind;
    }

    public string Kind { get; }

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

    public string Name { get; protected set; }

    public string TypeName { get; protected set; }

    public string Documentation { get; protected set; }

    public string DisplayName { get; protected set; }

    public bool CaseSensitive
    {
        get => HasFlag(Flags.CaseSensitive);
        protected set => SetOrClearFlag(Flags.CaseSensitive, value);
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

    public bool Equals(BoundAttributeParameterDescriptor other)
    {
        return BoundAttributeParameterDescriptorComparer.Default.Equals(this, other);
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as BoundAttributeParameterDescriptor);
    }

    public override int GetHashCode()
    {
        return BoundAttributeParameterDescriptorComparer.Default.GetHashCode(this);
    }
}
