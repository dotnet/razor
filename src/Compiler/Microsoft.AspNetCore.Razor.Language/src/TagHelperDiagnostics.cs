// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Microsoft.AspNetCore.Razor.Language;

internal static class TagHelperDiagnostics
{
    private static readonly ConditionalWeakTable<object, StrongBox<ImmutableArray<RazorDiagnostic>>> s_immutablediagnosticsTable = new();

    private static void AddDiagnosticsToTable(object obj, ImmutableArray<RazorDiagnostic> diagnostics)
    {
        if (diagnostics.Length > 0)
        {
            s_immutablediagnosticsTable.Add(obj, new(diagnostics));
        }
    }

    private static ImmutableArray<RazorDiagnostic> GetDiagnosticsFromTable(object obj)
        => s_immutablediagnosticsTable.TryGetValue(obj, out var box)
            ? box.Value
            : ImmutableArray<RazorDiagnostic>.Empty;

    public static void AddDiagnostics(AllowedChildTagDescriptor descriptor, ImmutableArray<RazorDiagnostic> diagnostics)
        => AddDiagnosticsToTable(descriptor, diagnostics);

    public static void AddDiagnostics(BoundAttributeDescriptor descriptor, ImmutableArray<RazorDiagnostic> diagnostics)
        => AddDiagnosticsToTable(descriptor, diagnostics);

    public static void AddDiagnostics(BoundAttributeParameterDescriptor descriptor, ImmutableArray<RazorDiagnostic> diagnostics)
        => AddDiagnosticsToTable(descriptor, diagnostics);

    public static void AddDiagnostics(RequiredAttributeDescriptor descriptor, ImmutableArray<RazorDiagnostic> diagnostics)
        => AddDiagnosticsToTable(descriptor, diagnostics);

    public static void AddDiagnostics(TagHelperDescriptor descriptor, ImmutableArray<RazorDiagnostic> diagnostics)
        => AddDiagnosticsToTable(descriptor, diagnostics);

    public static void AddDiagnostics(TagMatchingRuleDescriptor descriptor, ImmutableArray<RazorDiagnostic> diagnostics)
        => AddDiagnosticsToTable(descriptor, diagnostics);

    public static ImmutableArray<RazorDiagnostic> GetDiagnostics(AllowedChildTagDescriptor descriptor)
        => GetDiagnosticsFromTable(descriptor);

    public static ImmutableArray<RazorDiagnostic> GetDiagnostics(BoundAttributeDescriptor descriptor)
        => GetDiagnosticsFromTable(descriptor);

    public static ImmutableArray<RazorDiagnostic> GetDiagnostics(BoundAttributeParameterDescriptor descriptor)
        => GetDiagnosticsFromTable(descriptor);

    public static ImmutableArray<RazorDiagnostic> GetDiagnostics(RequiredAttributeDescriptor descriptor)
        => GetDiagnosticsFromTable(descriptor);

    public static ImmutableArray<RazorDiagnostic> GetDiagnostics(TagHelperDescriptor descriptor)
        => GetDiagnosticsFromTable(descriptor);

    public static ImmutableArray<RazorDiagnostic> GetDiagnostics(TagMatchingRuleDescriptor descriptor)
        => GetDiagnosticsFromTable(descriptor);
}
