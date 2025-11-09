// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.Razor.Completion;

internal record DirectiveAttributeCompletionContext(
    string SelectedAttributeName = "",
    string? SelectedParameterName = null,
    ImmutableArray<string> ExistingAttributes = default,
    bool UseSnippets = true,
    bool InAttributeName = true,
    bool InParameterName = false,
    RazorCompletionOptions Options = default)
{
}
