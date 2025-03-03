// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor.Cohost;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.Completion.Delegation;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using RoslynVSInternalCompletionItem = Roslyn.LanguageServer.Protocol.VSInternalCompletionItem;

namespace Microsoft.VisualStudio.Razor.LanguageClient.Cohost;

#pragma warning disable RS0030 // Do not use banned APIs
[Shared]
[CohostEndpoint(Methods.TextDocumentCompletionResolveName)]
[Export(typeof(IDynamicRegistrationProvider))]
[ExportCohostStatelessLspService(typeof(CohostDocumentCompletionResolveEndpoint))]
[method: ImportingConstructor]
#pragma warning restore RS0030 // Do not use banned APIs
internal sealed class CohostDocumentCompletionResolveEndpoint(
    CompletionListCache completionListCache,
    IHtmlDocumentSynchronizer htmlDocumentSynchronizer,
    LSPRequestInvoker requestInvoker,
    ILoggerFactory loggerFactory)
    : AbstractRazorCohostDocumentRequestHandler<RoslynVSInternalCompletionItem, RoslynVSInternalCompletionItem>, IDynamicRegistrationProvider
{
    private readonly IHtmlDocumentSynchronizer _htmlDocumentSynchronizer = htmlDocumentSynchronizer;
    private readonly LSPRequestInvoker _requestInvoker = requestInvoker;
    private readonly CompletionListCache _completionListCache = completionListCache;
    private readonly ILogger _logger = loggerFactory.GetOrCreateLogger<CohostDocumentCompletionEndpoint>();

    protected override bool MutatesSolutionState => false;

    protected override bool RequiresLSPSolution => true;

    public ImmutableArray<Registration> GetRegistrations(VSInternalClientCapabilities clientCapabilities, RazorCohostRequestContext requestContext)
    {
        if (clientCapabilities.TextDocument?.Completion?.DynamicRegistration is true)
        {
            return [new Registration()
            {
                Method = Methods.TextDocumentCompletionResolveName,
                RegisterOptions = new CompletionRegistrationOptions()
                {
                    ResolveProvider = true
                }
            }];
        }

        return [];
    }

    protected override RazorTextDocumentIdentifier? GetRazorTextDocumentIdentifier(RoslynVSInternalCompletionItem request)
    {
        var completionResolveParams = CohostDocumentCompletionResolveParams.GetCohostDocumentCompletionResolveParams(request);
        return Roslyn.LanguageServer.Protocol.RoslynLspExtensions.ToRazorTextDocumentIdentifier(completionResolveParams.TextDocument);
    }

    protected override Task<RoslynVSInternalCompletionItem> HandleRequestAsync(RoslynVSInternalCompletionItem request, RazorCohostRequestContext context, CancellationToken cancellationToken)
        => HandleRequestAsync(request, context.TextDocument.AssumeNotNull(), cancellationToken);

    private Task<RoslynVSInternalCompletionItem> HandleRequestAsync(
        RoslynVSInternalCompletionItem request,
        TextDocument razorDocument,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(request);
        }

        if (CohostDocumentCompletionEndpoint.ToVsLSP<VSInternalCompletionItem>(request) is not VSInternalCompletionItem completionItem)
        {
            return Task.FromResult(request);
        }

        (var originalRequestContext, var containingCompletionList) = _completionListCache.GetOriginalData(completionItem);

        if (containingCompletionList is null || originalRequestContext is null)
        {
            // Couldn't find an associated completion list
            return Task.FromResult(request);
        }

        var languageKind = (RazorLanguageKind)originalRequestContext;

        if (languageKind == RazorLanguageKind.Html)
        {
            request.Data = DelegatedCompletionHelper.GetOriginalCompletionItemData(completionItem, containingCompletionList);
            _ = GetHtmlCompletionResolveAsync(request, completionItem, razorDocument, cancellationToken);
        }

        return Task.FromResult(request);
    }

    private async Task<VSInternalCompletionItem> GetHtmlCompletionResolveAsync(
        RoslynVSInternalCompletionItem request,
        VSInternalCompletionItem originalItem,
        TextDocument razorDocument,
        CancellationToken cancellationToken)
    {
        var htmlDocument = await _htmlDocumentSynchronizer.TryGetSynchronizedHtmlDocumentAsync(razorDocument, cancellationToken).ConfigureAwait(false);
        if (htmlDocument is null)
        {
            return originalItem;
        }

        _logger.LogDebug($"Resolving completion item {request.Label} for {htmlDocument.Uri}");

        var result = await _requestInvoker.ReinvokeRequestOnServerAsync<VSInternalCompletionItem, VSInternalCompletionItem>(
            htmlDocument.Buffer,
            Methods.TextDocumentCompletionResolveName,
            RazorLSPConstants.HtmlLanguageServerName,
            originalItem,
            cancellationToken).ConfigureAwait(false);

        return result?.Response ?? originalItem;
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(CohostDocumentCompletionResolveEndpoint instance)
    {
        public Task<RoslynVSInternalCompletionItem> HandleRequestAsync(
            RoslynVSInternalCompletionItem request,
            TextDocument razorDocument,
            CancellationToken cancellationToken)
                => instance.HandleRequestAsync(request, razorDocument, cancellationToken);
    }
}
