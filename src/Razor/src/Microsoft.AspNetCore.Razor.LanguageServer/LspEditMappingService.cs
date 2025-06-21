// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal sealed class LspEditMappingService(
    IDocumentMappingService documentMappingService,
    IFilePathService filePathService,
    IDocumentContextFactory documentContextFactory) : AbstractEditMappingService(documentMappingService, filePathService)
{
    private readonly IFilePathService _filePathService = filePathService;
    private readonly IDocumentContextFactory _documentContextFactory = documentContextFactory;

    protected override bool TryGetDocumentContext(IDocumentSnapshot contextDocumentSnapshot, Uri razorDocumentUri, VSProjectContext? projectContext, [NotNullWhen(true)] out DocumentContext? documentContext)
    {
        if (!_documentContextFactory.TryCreate(razorDocumentUri, projectContext, out documentContext))
        {
            return false;
        }

        return true;
    }

    protected override Task<Uri?> GetRazorDocumentUriAsync(IDocumentSnapshot contextDocumentSnapshot, Uri uri, CancellationToken cancellationToken)
    {
        return Task.FromResult<Uri?>(_filePathService.GetRazorDocumentUri(uri));
    }
}
