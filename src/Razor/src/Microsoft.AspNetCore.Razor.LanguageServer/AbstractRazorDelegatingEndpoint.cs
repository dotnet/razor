// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using OmniSharp.Extensions.JsonRpc;

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
        protected readonly ILogger _logger;

        protected AbstractRazorDelegatingEndpoint(
            DocumentContextFactory documentContextFactory,
            LanguageServerFeatureOptions languageServerFeatureOptions,
            RazorDocumentMappingService documentMappingService,
            ClientNotifierServiceBase languageServer,
            ILogger logger)
        {
            _documentContextFactory = documentContextFactory ?? throw new ArgumentNullException(nameof(documentContextFactory));
            _languageServerFeatureOptions = languageServerFeatureOptions ?? throw new ArgumentNullException(nameof(languageServerFeatureOptions));
            _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
            _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Override to provide behavior for razor contexts. Since the c#/html servers won't
        /// have any information for this context it doesn't need to be delegated
        /// </summary>
        protected abstract Task<TResponse?> HandleInRazorAsync(TRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// The delegated object to send to the <see cref="EndpointName"/>
        /// </summary>
        protected abstract Task<TDelegatedParams> CreateDelegatedParamsAsync(TRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// The name of the endpoint to delegate to, from <see cref="LanguageServerConstants"/>
        /// </summary>
        protected abstract string EndpointName { get; }

        /// <summary>
        /// If the response needs to be remapped for any reasons, override to handle the remapping logic
        /// </summary>
        protected virtual Task<TResponse> RemapResponseAsync(TResponse delegatedResponse, DocumentContext documentContext, CancellationToken cancellationToken)
            => Task.FromResult(delegatedResponse);

        public async Task<TResponse?> Handle(TRequest request, CancellationToken cancellationToken)
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

            if (projection.LanguageKind == RazorLanguageKind.Razor)
            {
                return await HandleInRazorAsync(request, cancellationToken).ConfigureAwait(false);
            }

            if (!_languageServerFeatureOptions.SingleServerSupport)
            {
                return null;
            }

            var delegatedParams = await CreateDelegatedParamsAsync(request, cancellationToken);

            var delegatedRequest = await _languageServer.SendRequestAsync(EndpointName, delegatedParams).ConfigureAwait(false);
            var delegatedResponse = await delegatedRequest.Returning<TResponse?>(cancellationToken).ConfigureAwait(false);

            return delegatedResponse is null
                ? delegatedResponse
                : await RemapResponseAsync(delegatedResponse, documentContext, cancellationToken).ConfigureAwait(false);
        }
    }
}
