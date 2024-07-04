// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol.CodeActions;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

internal sealed class RazorCodeActionContext(
    VSCodeActionParams request,
    IDocumentSnapshot documentSnapshot,
    RazorCodeDocument codeDocument,
    SourceLocation location,
    SourceText sourceText,
    bool supportsFileCreation,
    bool supportsCodeActionResolve)
{
    public VSCodeActionParams Request { get; } = request;
    public IDocumentSnapshot DocumentSnapshot { get; } = documentSnapshot;
    public RazorCodeDocument CodeDocument { get; } = codeDocument;
    public SourceLocation Location { get; } = location;
    public SourceText SourceText { get; } = sourceText;
    public bool SupportsFileCreation { get; } = supportsFileCreation;
    public bool SupportsCodeActionResolve { get; } = supportsCodeActionResolve;
}
