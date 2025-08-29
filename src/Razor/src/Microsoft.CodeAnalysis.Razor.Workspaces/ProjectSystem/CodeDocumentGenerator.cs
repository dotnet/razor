// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using Microsoft.AspNetCore.Razor.Language;

namespace Microsoft.CodeAnalysis.Razor.ProjectSystem;

internal readonly struct CodeDocumentGenerator(RazorProjectEngine projectEngine)
{
    public RazorCodeDocument Generate(
        RazorSourceDocument source,
        RazorFileKind fileKind,
        ImmutableArray<RazorSourceDocument> importSources,
        ImmutableArray<TagHelperDescriptor> tagHelpers,
        CancellationToken cancellationToken)
    {
        return projectEngine.Process(source, fileKind, importSources, tagHelpers, cancellationToken);
    }

    public RazorCodeDocument GenerateDesignTime(
        RazorSourceDocument source,
        RazorFileKind fileKind,
        ImmutableArray<RazorSourceDocument> importSources,
        ImmutableArray<TagHelperDescriptor> tagHelpers,
        CancellationToken cancellationToken)
    {
        return projectEngine.ProcessDesignTime(source, fileKind, importSources, tagHelpers, cancellationToken);
    }
}
