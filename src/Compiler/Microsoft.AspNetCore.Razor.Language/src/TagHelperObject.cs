// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;

namespace Microsoft.AspNetCore.Razor.Language;

public abstract class TagHelperObject
{
    public ImmutableArray<RazorDiagnostic> Diagnostics { get; }

    public bool HasErrors
        => Diagnostics.Any(static d => d.Severity == RazorDiagnosticSeverity.Error);

    private protected TagHelperObject(ImmutableArray<RazorDiagnostic> diagnostics)
    {
        Diagnostics = diagnostics.NullToEmpty();
    }
}
