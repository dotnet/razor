// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.MapCode;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer.MapCode;

internal sealed class LspMapCodeService(
    IDocumentMappingService documentMappingService,
    IDocumentContextFactory documentContextFactory) : AbstractMapCodeService(documentMappingService)
{
    private readonly IDocumentMappingService _documentMappingService = documentMappingService;
    private readonly IDocumentContextFactory _documentContextFactory = documentContextFactory;

    protected override bool TryCreateDocumentContext(ISolutionQueryOperations queryOperations, Uri uri, [NotNullWhen(true)] out DocumentContext? documentContext)
        => _documentContextFactory.TryCreate(uri, out documentContext);

    protected override Task<(Uri MappedDocumentUri, LinePositionSpan MappedRange)> MapToHostDocumentUriAndRangeAsync(DocumentContext documentContext, Uri generatedDocumentUri, LinePositionSpan generatedDocumentRange, CancellationToken cancellationToken)
        => _documentMappingService.MapToHostDocumentUriAndRangeAsync(generatedDocumentUri, generatedDocumentRange, cancellationToken);
}
