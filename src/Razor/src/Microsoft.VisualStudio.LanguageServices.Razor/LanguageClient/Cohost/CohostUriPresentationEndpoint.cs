﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Razor.LanguageClient.Extensions;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(VSInternalMethods.TextDocumentUriPresentationName)]
[Export(typeof(IDynamicRegistrationProvider))]
[ExportCohostStatelessLspService(typeof(CohostUriPresentationEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal class CohostUriPresentationEndpoint(IRemoteClientProvider remoteClientProvider, ILoggerFactory loggerFactory)
    : AbstractRazorCohostDocumentRequestHandler<VSInternalUriPresentationParams, WorkspaceEdit?>, IDynamicRegistrationProvider
{
    private readonly IRemoteClientProvider _remoteClientProvider = remoteClientProvider;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<CohostUriPresentationEndpoint>();

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    public Registration? GetRegistration(VSInternalClientCapabilities clientCapabilities, DocumentFilter[] filter, RazorCohostRequestContext requestContext)
    {
        if (clientCapabilities.SupportsVisualStudioExtensions)
        {
            return new Registration
            {
                Method = VSInternalMethods.TextDocumentUriPresentationName,
                RegisterOptions = new TextDocumentRegistrationOptions()
                {
                    DocumentSelector = filter
                }
            };
        }

        return null;
    }

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(VSInternalUriPresentationParams request)
        => request.TextDocument.ToRazorTextDocumentIdentifier();

    protected override async Task<WorkspaceEdit?> HandleRequestAsync(VSInternalUriPresentationParams request, RazorCohostRequestContext context, CancellationToken cancellationToken)
    {
        var razorDocument = context.TextDocument.AssumeNotNull();

        var remoteClient = await _remoteClientProvider.TryGetClientAsync(cancellationToken).ConfigureAwait(false);
        if (remoteClient is null)
        {
            _logger.LogWarning($"Couldn't get remote client");
            return null;
        }

        try
        {
            var data = await remoteClient.TryInvokeAsync<IRemoteUriPresentationService, TextChange?>(
                razorDocument.Project.Solution,
                (service, solutionInfo, cancellationToken) => service.GetPresentationAsync(solutionInfo, razorDocument.Id, request.Range.ToLinePositionSpan(), request.Uris, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            if (data.Value is { } textChange)
            {
                var sourceText = await razorDocument.GetTextAsync(cancellationToken).ConfigureAwait(false);

                return new WorkspaceEdit
                {
                    DocumentChanges = new TextDocumentEdit[]
                    {
                        new TextDocumentEdit
                        {
                            TextDocument = new()
                            {
                                Uri = request.TextDocument.Uri
                            },
                            Edits = [textChange.ToTextEdit(sourceText)]
                        }
                    }
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error calling remote");
            return null;
        }

        return null;
    }
}
