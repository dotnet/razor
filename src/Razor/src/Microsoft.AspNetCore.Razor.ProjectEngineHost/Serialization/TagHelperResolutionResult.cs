// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Serialization;

internal sealed class TagHelperResolutionResult
{
    internal static readonly TagHelperResolutionResult Empty = new(Array.Empty<TagHelperDescriptor>());

    public IReadOnlyCollection<TagHelperDescriptor> Descriptors { get; }

    public TagHelperResolutionResult(IReadOnlyCollection<TagHelperDescriptor>? descriptors)
    {
        Descriptors = descriptors ?? Array.Empty<TagHelperDescriptor>();
    }
}
