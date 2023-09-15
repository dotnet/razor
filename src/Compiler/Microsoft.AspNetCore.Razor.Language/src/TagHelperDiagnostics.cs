// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Microsoft.AspNetCore.Razor.Language;

internal static class TagHelperDiagnostics
{
    private static readonly ConditionalWeakTable<object, IReadOnlyList<RazorDiagnostic>> s_diagnosticsTable = new();
    private static readonly ConditionalWeakTable<object, StrongBox<ImmutableArray<RazorDiagnostic>>> s_immutablediagnosticsTable = new();

    private static void AddDiagnosticsToTable(object obj, IReadOnlyList<RazorDiagnostic> diagnostics)
    {
        if (diagnostics.Count > 0)
        {
            s_diagnosticsTable.Add(obj, diagnostics);
        }
    }

    private static void AddImmutableDiagnosticsToTable(object obj, ImmutableArray<RazorDiagnostic> diagnostics)
    {
        if (diagnostics.Length > 0)
        {
            s_immutablediagnosticsTable.Add(obj, new(diagnostics));
        }
    }

    private static IReadOnlyList<RazorDiagnostic> GetDiagnosticsFromTable(object obj)
        => s_diagnosticsTable.TryGetValue(obj, out var diagnostics)
            ? diagnostics
            : Array.Empty<RazorDiagnostic>();

    private static ImmutableArray<RazorDiagnostic> GetImmutableDiagnosticsFromTable(object obj)
        => s_immutablediagnosticsTable.TryGetValue(obj, out var box)
            ? box.Value
            : ImmutableArray<RazorDiagnostic>.Empty;

    private static void RemoveDiagnosticsFromTable(object obj)
        => s_diagnosticsTable.Remove(obj);

    public static void AddDiagnostics(AllowedChildTagDescriptor descriptor, ImmutableArray<RazorDiagnostic> diagnostics)
        => AddImmutableDiagnosticsToTable(descriptor, diagnostics);

    public static void AddDiagnostics(BoundAttributeDescriptor descriptor, ImmutableArray<RazorDiagnostic> diagnostics)
        => AddImmutableDiagnosticsToTable(descriptor, diagnostics);

    public static void AddDiagnostics(BoundAttributeParameterDescriptor descriptor, ImmutableArray<RazorDiagnostic> diagnostics)
        => AddImmutableDiagnosticsToTable(descriptor, diagnostics);

    public static void AddDiagnostics(RequiredAttributeDescriptor descriptor, ImmutableArray<RazorDiagnostic> diagnostics)
        => AddImmutableDiagnosticsToTable(descriptor, diagnostics);

    public static void AddDiagnostics(TagHelperDescriptor descriptor, IReadOnlyList<RazorDiagnostic> diagnostics)
        => AddDiagnosticsToTable(descriptor, diagnostics);

    public static void AddDiagnostics(TagMatchingRuleDescriptor descriptor, IReadOnlyList<RazorDiagnostic> diagnostics)
        => AddDiagnosticsToTable(descriptor, diagnostics);

    public static ImmutableArray<RazorDiagnostic> GetDiagnostics(AllowedChildTagDescriptor descriptor)
        => GetImmutableDiagnosticsFromTable(descriptor);

    public static ImmutableArray<RazorDiagnostic> GetDiagnostics(BoundAttributeDescriptor descriptor)
        => GetImmutableDiagnosticsFromTable(descriptor);

    public static ImmutableArray<RazorDiagnostic> GetDiagnostics(BoundAttributeParameterDescriptor descriptor)
        => GetImmutableDiagnosticsFromTable(descriptor);

    public static ImmutableArray<RazorDiagnostic> GetDiagnostics(RequiredAttributeDescriptor descriptor)
        => GetImmutableDiagnosticsFromTable(descriptor);

    public static IReadOnlyList<RazorDiagnostic> GetDiagnostics(TagHelperDescriptor descriptor)
        => GetDiagnosticsFromTable(descriptor);

    public static IReadOnlyList<RazorDiagnostic> GetDiagnostics(TagMatchingRuleDescriptor descriptor)
        => GetDiagnosticsFromTable(descriptor);

    public static void RemoveDiagnostics(TagHelperDescriptor descriptor)
        => RemoveDiagnosticsFromTable(descriptor);

    public static void RemoveDiagnostics(TagMatchingRuleDescriptor descriptor)
        => RemoveDiagnosticsFromTable(descriptor);
}
