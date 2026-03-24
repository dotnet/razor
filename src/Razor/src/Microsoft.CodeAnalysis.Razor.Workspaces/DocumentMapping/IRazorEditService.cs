// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Razor.DocumentMapping;

internal interface IRazorEditService
{
    Task<ImmutableArray<RazorTextChange>> MapCSharpEditsAsync(
        ImmutableArray<RazorTextChange> textChanges,
        IDocumentSnapshot snapshot,
        CancellationToken cancellationToken);

    Task<ImmutableArray<RazorTextChange>> GetEditsForCSharpLanguageFeaturesAsync(
        IDocumentSnapshot snapshot,
        SourceText changedCSharpText,
        CancellationToken cancellationToken);
}
