// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;

namespace Microsoft.CodeAnalysis.Razor.Cohost;

internal interface IIncompatibleProjectService
{
    void HandleMiscellaneousFile(TextDocument textDocument);
    void HandleNullDocument(RazorTextDocumentIdentifier? textDocumentIdentifier, RazorCohostRequestContext context);
}
