﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Telemetry;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost.Handlers;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.SemanticTokens;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Remote.Razor.SemanticTokens;

[Export(typeof(ICSharpSemanticTokensProvider)), Shared]
[method: ImportingConstructor]
internal class RemoteCSharpSemanticTokensProvider(IFilePathService filePathService, ITelemetryReporter telemetryReporter) : ICSharpSemanticTokensProvider
{
    private readonly IFilePathService _filePathService = filePathService;
    private readonly ITelemetryReporter _telemetryReporter = telemetryReporter;

    public async Task<int[]?> GetCSharpSemanticTokensResponseAsync(VersionedDocumentContext documentContext, ImmutableArray<LinePositionSpan> csharpRanges, Guid correlationId, CancellationToken cancellationToken)
    {
        using var _ = _telemetryReporter.TrackLspRequest(nameof(SemanticTokensRange.GetSemanticTokensAsync), Constants.ExternalAccessServerName, correlationId);

        // We have a razor document, lets find the generated C# document
        var generatedDocument = await documentContext.GetGeneratedDocumentAsync(_filePathService, cancellationToken).ConfigureAwait(false);

        var data = await SemanticTokensRange.GetSemanticTokensAsync(
            generatedDocument,
            csharpRanges,
            supportsVisualStudioExtensions: true,
            cancellationToken).ConfigureAwait(false);

        return data;
    }
}
