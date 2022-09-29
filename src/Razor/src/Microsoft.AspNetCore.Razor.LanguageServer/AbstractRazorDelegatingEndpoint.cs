// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using OmniSharp.Extensions.JsonRpc;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal abstract class AbstractRazorDelegatingEndpoint<TRequest, TResponse> : IJsonRpcRequestHandler<TRequest, TResponse?>
        where TRequest : ITextDocumentPositionParams, IRequest<TResponse?>
    {
        private readonly DocumentContextFactory _documentContextFactory;
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly ClientNotifierServiceBase _languageServer;

        protected readonly LanguageServerFeatureOptions LanguageServerFeatureOptions;
        protected readonly ILogger Logger;

        protected AbstractRazorDelegatingEndpoint(
            DocumentContextFactory documentContextFactory,
            LanguageServerFeatureOptions languageServerFeatureOptions,
            RazorDocumentMappingService documentMappingService,
            ClientNotifierServiceBase languageServer,
            ILogger logger)
        {
            _documentContextFactory = documentContextFactory ?? throw new ArgumentNullException(nameof(documentContextFactory));
            _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
            _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));

            LanguageServerFeatureOptions = languageServerFeatureOptions ?? throw new ArgumentNullException(nameof(languageServerFeatureOptions));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// The delegated object to send to the <see cref="CustomMessageTarget"/>
        /// </summary>
        protected abstract IDelegatedParams? CreateDelegatedParams(TRequest request, DocumentContext documentContext, Projection projection, CancellationToken cancellationToken);

        /// <summary>
        /// The name of the endpoint to delegate to, from <see cref="RazorLanguageServerCustomMessageTargets"/>. This is the
        /// custom endpoint that is sent via <see cref="ClientNotifierServiceBase"/> which returns
        /// a response by delegating to C#/HTML.
        /// </summary>
        /// <remarks>
        /// An example is <see cref="RazorLanguageServerCustomMessageTargets.RazorHoverEndpointName"/>
        /// </remarks>
        protected abstract string CustomMessageTarget { get; }

        /// <summary>
        /// If the response needs to be handled, such as for remapping positions back, override and handle here
        /// </summary>
        protected virtual Task<TResponse?> HandleDelegatedResponseAsync(TResponse? delegatedResponse, TRequest originalRequest, DocumentContext documentContext, Projection projection, CancellationToken cancellationToken)
            => Task.FromResult(delegatedResponse);

        /// <summary>
        /// If the request can be handled without delegation, override this to provide a response. If a null
        /// value is returned the request will be delegated to C#/HTML servers, otherwise the response
        /// will be used in <see cref="Handle(TRequest, CancellationToken)"/>
        /// </summary>
        protected virtual Task<TResponse?> TryHandleAsync(TRequest request, DocumentContext documentContext, Projection projection, CancellationToken cancellationToken)
            => Task.FromResult<TResponse?>(default);

        /// <summary>
        /// Returns true if the configuration supports this operation being handled, otherwise returns false. Use to
        /// handle cases where <see cref="CodeAnalysis.Razor.Workspaces.LanguageServerFeatureOptions"/> other than <see cref="LanguageServerFeatureOptions.SingleServerSupport"/>
        /// need to be checked to validate that the operation can be done.
        /// </summary>
        protected virtual bool IsSupported() => true;

        /// <summary>
        /// Implementation for <see cref="IRequest{TResponse}"/>
        /// </summary>
        public async Task<TResponse?> Handle(TRequest request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!IsSupported())
            {
                return default;
            }

            var documentContext = await _documentContextFactory.TryCreateAsync(request.TextDocument.Uri, cancellationToken).ConfigureAwait(false);
            if (documentContext is null)
            {
                return default;
            }

            var projection = await _documentMappingService.TryGetProjectionAsync(documentContext, request.Position, Logger, cancellationToken).ConfigureAwait(false);
            if (projection is null)
            {
                return default;
            }

            var response = await TryHandleAsync(request, documentContext, projection, cancellationToken).ConfigureAwait(false);
            // If TResponse is a SumType<T1,T2,...> then it might be non-null, but the value within it could be, so we have to check
            // for that too.
            if (response is not null && response is not ISumType { Value: null })
            {
                return response;
            }

            if (!LanguageServerFeatureOptions.SingleServerSupport)
            {
                return default;
            }

            // We can only delegate to C# and HTML, so if we're in a Razor context and our inheritor didn't want to provide
            // any response then that's all we can do.
            if (projection.LanguageKind == RazorLanguageKind.Razor)
            {
                return default;
            }

            var delegatedParams = CreateDelegatedParams(request, documentContext, projection, cancellationToken);

            if (delegatedParams is null)
            {
                // I guess they don't want to delegate... fine then!
                return default;
            }

            var delegatedRequest = await _languageServer.SendRequestAsync(CustomMessageTarget, delegatedParams).ConfigureAwait(false);
            if (delegatedRequest is null)
            {
                return default;
            }

            var delegatedResponse = await delegatedRequest.Returning<TResponse?>(cancellationToken).ConfigureAwait(false);
            if (delegatedResponse is null)
            {
                return default;
            }

            var remappedResponse = await HandleDelegatedResponseAsync(delegatedResponse, request, documentContext, projection, cancellationToken).ConfigureAwait(false);
            return remappedResponse;
        }
    }
}
