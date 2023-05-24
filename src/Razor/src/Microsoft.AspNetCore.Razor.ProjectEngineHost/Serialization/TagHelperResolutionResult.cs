// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal sealed class TagHelperResolutionResult
{
    internal static readonly TagHelperResolutionResult Empty = new(Array.Empty<TagHelperDescriptor>(), ImmutableArray<RazorDiagnostic>.Empty);

    public IReadOnlyCollection<TagHelperDescriptor> Descriptors { get; }
    public ImmutableArray<RazorDiagnostic> Diagnostics { get; }

    public TagHelperResolutionResult(IReadOnlyCollection<TagHelperDescriptor>? descriptors, ImmutableArray<RazorDiagnostic> diagnostics)
    {
        Descriptors = descriptors ?? Array.Empty<TagHelperDescriptor>();
        Diagnostics = diagnostics.NullToEmpty();
    }
}
