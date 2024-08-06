// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal sealed class LspDocumentMappingService(
    IFilePathService filePathService,
    IDocumentContextFactory documentContextFactory,
    ILoggerFactory loggerFactory)
    : AbstractDocumentMappingService(filePathService, loggerFactory.GetOrCreateLogger<LspDocumentMappingService>())
{
    private readonly IDocumentContextFactory _documentContextFactory = documentContextFactory;

    protected override async ValueTask<RazorCodeDocument?> TryGetCodeDocumentAsync(Uri razorDocumentUri, CancellationToken cancellationToken)
    {
        if (!_documentContextFactory.TryCreate(razorDocumentUri, out var documentContext))
        {
            return null;
        }

        return await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
    }
}
