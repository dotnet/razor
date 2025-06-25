﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.Test.Common;

internal class TestTagHelperResolver(ImmutableArray<TagHelperDescriptor> tagHelpers) : ITagHelperResolver
{
    public ImmutableArray<TagHelperDescriptor> TagHelpers { get; } = tagHelpers;

    public ValueTask<ImmutableArray<TagHelperDescriptor>> GetTagHelpersAsync(
        Project workspaceProject,
        ProjectSnapshot projectSnapshot,
        CancellationToken cancellationToken)
    {
        return new(TagHelpers);
    }
}
