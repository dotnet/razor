﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;

namespace Microsoft.CodeAnalysis.Razor.Cohost;

internal interface IIncompatibleProjectService
{
    void HandleMiscFilesDocument(TextDocument textDocument);
    void HandleMissingDocument(RazorTextDocumentIdentifier? textDocumentIdentifier, RazorCohostRequestContext context);
}
