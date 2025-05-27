﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Features;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(Methods.TextDocumentDefinitionName)]
[Export(typeof(IDynamicRegistrationProvider))]
[ExportRazorStatelessLspService(typeof(CohostGoToDefinitionEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostGoToDefinitionEndpoint(
    IRemoteServiceInvoker remoteServiceInvoker,
    IHtmlRequestInvoker requestInvoker,
    IFilePathService filePathService)
    : AbstractRazorCohostDocumentRequestHandler<TextDocumentPositionParams, SumType<LspLocation, LspLocation[], DocumentLink[]>?>, IDynamicRegistrationProvider
{
    private readonly IRemoteServiceInvoker _remoteServiceInvoker = remoteServiceInvoker;
    private readonly IHtmlRequestInvoker _requestInvoker = requestInvoker;
    private readonly IFilePathService _filePathService = filePathService;

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    public ImmutableArray<Registration> GetRegistrations(VSInternalClientCapabilities clientCapabilities, RazorCohostRequestContext requestContext)
    {
        if (clientCapabilities.TextDocument?.Definition?.DynamicRegistration == true)
        {
            return [new Registration
            {
                Method = Methods.TextDocumentDefinitionName,
                RegisterOptions = new DefinitionRegistrationOptions()
            }];
        }

        return [];
    }

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(TextDocumentPositionParams request)
        => request.TextDocument.ToRazorTextDocumentIdentifier();

    protected override Task<SumType<LspLocation, LspLocation[], DocumentLink[]>?> HandleRequestAsync(TextDocumentPositionParams request, RazorCohostRequestContext context, CancellationToken cancellationToken)
        => HandleRequestAsync(
            request,
            context.TextDocument.AssumeNotNull(),
            cancellationToken);

    private async Task<SumType<LspLocation, LspLocation[], DocumentLink[]>?> HandleRequestAsync(TextDocumentPositionParams request, TextDocument razorDocument, CancellationToken cancellationToken)
    {
        var position = LspFactory.CreatePosition(request.Position.ToLinePosition());

        var response = await _remoteServiceInvoker
            .TryInvokeAsync<IRemoteGoToDefinitionService, RemoteResponse<LspLocation[]?>>(
                razorDocument.Project.Solution,
                (service, solutionInfo, cancellationToken) =>
                    service.GetDefinitionAsync(solutionInfo, razorDocument.Id, position, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);

        if (response.Result is LspLocation[] locations)
        {
            return locations;
        }

        if (response.StopHandling)
        {
            return null;
        }

        return await GetHtmlDefinitionsAsync(request, razorDocument, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SumType<LspLocation, LspLocation[], DocumentLink[]>?> GetHtmlDefinitionsAsync(TextDocumentPositionParams request, TextDocument razorDocument, CancellationToken cancellationToken)
    {
        var result = await _requestInvoker
            .MakeHtmlLspRequestAsync<TextDocumentPositionParams, SumType<LspLocation, LspLocation[], DocumentLink[]>>(
                razorDocument,
                Methods.TextDocumentDefinitionName,
                request,
                cancellationToken)
            .ConfigureAwait(false);

        if (result.Value is null)
        {
            return null;
        }

        if (result.TryGetFirst(out var singleLocation))
        {
            return LspFactory.CreateLocation(RemapVirtualHtmlUri(singleLocation.Uri), singleLocation.Range.ToLinePositionSpan());
        }
        else if (result.TryGetSecond(out var multipleLocations))
        {
            return Array.ConvertAll(multipleLocations, l => LspFactory.CreateLocation(RemapVirtualHtmlUri(l.Uri), l.Range.ToLinePositionSpan()));
        }
        else if (result.TryGetThird(out var documentLinks))
        {
            using var builder = new PooledArrayBuilder<DocumentLink>(capacity: documentLinks.Length);

            foreach (var documentLink in documentLinks)
            {
                if (documentLink.Target is Uri target)
                {
                    builder.Add(LspFactory.CreateDocumentLink(RemapVirtualHtmlUri(target), documentLink.Range.ToLinePositionSpan()));
                }
            }

            return builder.ToArray();
        }

        return null;
    }

    private Uri RemapVirtualHtmlUri(Uri uri)
    {
        if (_filePathService.IsVirtualHtmlFile(uri))
        {
            return _filePathService.GetRazorDocumentUri(uri);
        }

        return uri;
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostGoToDefinitionEndpoint instance)
    {
        public Task<SumType<LspLocation, LspLocation[], DocumentLink[]>?> HandleRequestAsync(
            TextDocumentPositionParams request, TextDocument razorDocument, CancellationToken cancellationToken)
            => instance.HandleRequestAsync(request, razorDocument, cancellationToken);
    }
}
