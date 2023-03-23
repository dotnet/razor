// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Razor.PooledObjects;

namespace Microsoft.AspNetCore.Razor.Language;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public abstract class TagHelperDescriptor : IEquatable<TagHelperDescriptor>
{
    private enum Flags : byte
    {
        ContainsDiagnostics = 0x01,
        IsComponentFullyQualifiedNameMatchCacheSet = 0x02,
        IsComponentFullyQualifiedNameMatchCache = 0x04,
        IsChildContentTagHelperCacheSet = 0x08,
        IsChildContentTagHelperCache = 0x10,
        CaseSensitive = 0x20
    }

    private Flags _flags;
    private int? _hashCode;

    private IEnumerable<RazorDiagnostic> _allDiagnostics;
    private BoundAttributeDescriptor[] _editorRequiredAttributes;

    protected TagHelperDescriptor(string kind)
    {
        Kind = kind;
    }

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

    public string Kind { get; }

    public string Name { get; protected set; }

    public IReadOnlyList<TagMatchingRuleDescriptor> TagMatchingRules { get; protected set; }

    public string AssemblyName { get; protected set; }

    public IReadOnlyList<BoundAttributeDescriptor> BoundAttributes { get; protected set; }

    public IReadOnlyList<AllowedChildTagDescriptor> AllowedChildTags { get; protected set; }

    public string Documentation { get; protected set; }

    public string DisplayName { get; protected set; }

    public string TagOutputHint { get; protected set; }

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

    internal bool? IsComponentFullyQualifiedNameMatchCache
    {
        get => GetTriStateFlags(Flags.IsComponentFullyQualifiedNameMatchCacheSet, Flags.IsComponentFullyQualifiedNameMatchCache);
        set => UpdateTriStateFlags(value, Flags.IsComponentFullyQualifiedNameMatchCacheSet, Flags.IsComponentFullyQualifiedNameMatchCache);
    }

    internal bool? IsChildContentTagHelperCache
    {
        get => GetTriStateFlags(Flags.IsChildContentTagHelperCache, Flags.IsChildContentTagHelperCacheSet);
        set => UpdateTriStateFlags(value, Flags.IsChildContentTagHelperCache, Flags.IsChildContentTagHelperCacheSet);
    }

    private bool? GetTriStateFlags(Flags isSet, Flags isOn)
        => HasFlag(isSet)
            ? HasFlag(isOn)
            : null;

    private void UpdateTriStateFlags(bool? value, Flags isSet, Flags isOn)
    {
        switch (value)
        {
            case true:
                SetFlag(isOn);
                SetFlag(isSet);
                break;

            case false:
                ClearFlag(isOn);
                SetFlag(isSet);
                break;

            case null:
                ClearFlag(isSet);
                break;
        }
    }

    internal BoundAttributeDescriptor[] EditorRequiredAttributes
    {
        get
        {
            _editorRequiredAttributes ??= GetEditorRequiredAttributes(BoundAttributes);
            return _editorRequiredAttributes;

            static BoundAttributeDescriptor[] GetEditorRequiredAttributes(IReadOnlyList<BoundAttributeDescriptor> attributes)
            {
                var count = attributes.Count;

                if (count == 0)
                {
                    return Array.Empty<BoundAttributeDescriptor>();
                }

                using var results = new PooledList<BoundAttributeDescriptor>();

                for (var i = 0; i < count; i++)
                {
                    if (attributes[i] is { IsEditorRequired: true } editorRequiredAttribute)
                    {
                        results.Add(editorRequiredAttribute);
                    }
                }

                return results.ToArray();
            }
        }
    }

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

    public virtual IEnumerable<RazorDiagnostic> GetAllDiagnostics()
    {
        if (_allDiagnostics == null)
        {
            using var diagnostics = new PooledList<RazorDiagnostic>();

            foreach (var allowedChildTag in AllowedChildTags)
            {
                diagnostics.AddRange(allowedChildTag.Diagnostics);
            }

            foreach (var attribute in BoundAttributes)
            {
                diagnostics.AddRange(attribute.Diagnostics);
            }

            // BUG?: Diagnostics on BoundAttributeParameterDescriptors are not collected here.

            foreach (var rule in TagMatchingRules)
            {
                diagnostics.AddRange(rule.GetAllDiagnostics());
            }

            diagnostics.AddRange(Diagnostics);

            _allDiagnostics = diagnostics.ToArray();
        }

        return _allDiagnostics;
    }

    public override string ToString()
    {
        return DisplayName ?? base.ToString();
    }

    public bool Equals(TagHelperDescriptor other)
    {
        return TagHelperDescriptorComparer.Default.Equals(this, other);
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as TagHelperDescriptor);
    }

    public override int GetHashCode()
    {
        // TagHelperDescriptors are immutable instances and it should be safe to cache it's hashes once.
        return _hashCode ??= TagHelperDescriptorComparer.Default.GetHashCode(this);
    }

    private string GetDebuggerDisplay()
    {
        return $"{DisplayName} - {string.Join(" | ", TagMatchingRules.Select(r => r.GetDebuggerDisplay()))}";
    }
}
