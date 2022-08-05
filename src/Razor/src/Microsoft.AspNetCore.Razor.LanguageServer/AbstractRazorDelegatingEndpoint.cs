// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Hover;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using OmniSharp.Extensions.JsonRpc;
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal abstract class AbstractRazorDelegatingEndpoint<TRequest, TResponse, TDelegatedParams> : IJsonRpcRequestHandler<TRequest, TResponse?>
        where TRequest : TextDocumentPositionParams, IRequest<TResponse>
        where TResponse : class?
        where TDelegatedParams : class
    {
        private readonly DocumentContextFactory _documentContextFactory;
        private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly ClientNotifierServiceBase _languageServer;
        private readonly ILogger<RazorHoverEndpoint> _logger;

        protected AbstractRazorDelegatingEndpoint(
            DocumentContextFactory documentContextFactory,
            LanguageServerFeatureOptions languageServerFeatureOptions,
            RazorDocumentMappingService documentMappingService,
            ClientNotifierServiceBase languageServer,
            ILoggerFactory loggerFactory)
        {
            _documentContextFactory = documentContextFactory ?? throw new ArgumentNullException(nameof(documentContextFactory));
            _languageServerFeatureOptions = languageServerFeatureOptions ?? throw new ArgumentNullException(nameof(languageServerFeatureOptions));
            _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
            _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
            _logger = loggerFactory?.CreateLogger<RazorHoverEndpoint>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        /// <summary>
        /// Override to provide fallback behavior for when single server is not supported
        /// </summary>
        protected abstract Task<TResponse?> HandleWithoutSingleServerAsync(TRequest request, DocumentContext documentContext, RazorCodeDocument codeDocument, SourceText sourceText, CancellationToken cancellationToken);

        /// <summary>
        /// Override to provide behavior for razor contexts. Since the c#/html servers won't
        /// have any information for this context it doesn't need to be delegated
        /// </summary>
        protected abstract Task<TResponse?> HandleInRazorAsync(TRequest request, DocumentContext documentContext, RazorCodeDocument codeDocument, SourceText sourceText, CancellationToken cancellationToken);

        /// <summary>
        /// The delegated object to send to the <see cref="EndpointName"/>
        /// </summary>
        protected abstract TDelegatedParams CreateDelegatedParams(TRequest request, DocumentContext documentContext, Projection projection);

        /// <summary>
        /// The name of the endpoint to delegate to, from <see cref="LanguageServerConstants"/>
        /// </summary>
        protected abstract string EndpointName { get; }

        /// <summary>
        /// If the response needs to be remapped for any reasons, override to handle the remapping logic
        /// </summary>
        protected virtual Task<TResponse> RemapResponseAsync(TResponse delegatedResponse, RazorCodeDocument codeDocument, CancellationToken cancellationToken)
            => Task.FromResult(delegatedResponse);

        /// <summary>
        /// Extracts information about the request and then calls <see cref="GetDelegatedResponseAsync(TRequest, DocumentContext, RazorCodeDocument, SourceText, int, Projection, CancellationToken)"/>.
        /// Children can override to provide any more custom logic needed before doing a normal delegated response pass with <see cref="GetDelegatedResponseAsync(TRequest, DocumentContext, RazorCodeDocument, SourceText, int, Projection, CancellationToken)"/>
        /// </summary>
        public virtual async Task<TResponse?> Handle(TRequest request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var documentContext = await _documentContextFactory.TryCreateAsync(request.TextDocument.Uri, cancellationToken).ConfigureAwait(false);
            if (documentContext is null)
            {
                return null;
            }

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken);
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
            if (!request.Position.TryGetAbsoluteIndex(sourceText, _logger, out var absoluteIndex))
            {
                return null;
            }

            var projection = await _documentMappingService.GetProjectionAsync(documentContext, absoluteIndex, cancellationToken).ConfigureAwait(false);

            return await GetDelegatedResponseAsync(request, documentContext, codeDocument, sourceText, absoluteIndex, projection, cancellationToken).ConfigureAwait(false);
        }

        protected async Task<TResponse?> GetDelegatedResponseAsync(
            TRequest request,
            DocumentContext documentContext,
            RazorCodeDocument codeDocument,
            SourceText sourceText,
            int absoluteIndex,
            Projection projection,
            CancellationToken cancellationToken)
        {
            if (!_languageServerFeatureOptions.SingleServerSupport)
            {
                return await HandleWithoutSingleServerAsync(request, documentContext, codeDocument, sourceText, cancellationToken).ConfigureAwait(false);
            }

            if (projection.LanguageKind == RazorLanguageKind.Razor)
            {
                return await HandleInRazorAsync(request, documentContext, codeDocument, sourceText, cancellationToken).ConfigureAwait(false);
            }

            var delegatedParams = CreateDelegatedParams(request, documentContext, projection);

            var delegatedRequest = await _languageServer.SendRequestAsync(EndpointName, delegatedParams).ConfigureAwait(false);
            var delegatedResponse = await delegatedRequest.Returning<TResponse?>(cancellationToken).ConfigureAwait(false);

            return delegatedResponse is null
                ? delegatedResponse
                : await RemapResponseAsync(delegatedResponse, codeDocument, cancellationToken).ConfigureAwait(false);
        }

    }
}
