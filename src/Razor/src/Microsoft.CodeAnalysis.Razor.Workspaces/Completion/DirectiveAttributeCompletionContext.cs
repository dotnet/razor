// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;

namespace Microsoft.CodeAnalysis.Razor.Completion;

internal record DirectiveAttributeCompletionContext
{
    public string SelectedAttributeName { get; init; }
    public string? SelectedParameterName { get; init; }
    public ImmutableArray<string> ExistingAttributes { get; init; }
    public bool UseSnippets { get; init; }
    public bool InAttributeName { get; init; }
    public bool InParameterName { get; init; }
    public RazorCompletionOptions Options { get; init; }

    public DirectiveAttributeCompletionContext(
        string selectedAttributeName = "",
        string? selectedParameterName = null,
        ImmutableArray<string> existingAttributes = default,
        bool useSnippets = true,
        bool inAttributeName = true,
        bool inParameterName = false,
        RazorCompletionOptions options = default)
    {
        SelectedAttributeName = selectedAttributeName;
        SelectedParameterName = selectedParameterName;
        ExistingAttributes = existingAttributes.IsDefault ? [] : existingAttributes;
        UseSnippets = useSnippets;
        InAttributeName = inAttributeName;
        InParameterName = inParameterName;
        Options = options;
    }
}
