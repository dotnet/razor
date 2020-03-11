// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Threading;
using OmniSharpPosition = OmniSharp.Extensions.LanguageServer.Protocol.Models.Position;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    internal abstract class RazorLSPHandlerBase
    {
        private readonly int UndefinedDocumentVersion = -1;

        public RazorLSPHandlerBase(
            JoinableTaskContext joinableTaskContext,
            ILanguageClientBroker languageClientBroker,
            LSPDocumentManager documentManager,
            LSPDocumentSynchronizer documentSynchronizer)
        {
            if (joinableTaskContext is null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            if (languageClientBroker is null)
            {
                throw new ArgumentNullException(nameof(languageClientBroker));
            }

            if (documentManager is null)
            {
                throw new ArgumentNullException(nameof(documentManager));
            }

            if (documentSynchronizer is null)
            {
                throw new ArgumentNullException(nameof(documentSynchronizer));
            }

            JoinableTaskFactory = joinableTaskContext.Factory;
            LanguageClientBroker = languageClientBroker;
            DocumentManager = documentManager;
            DocumentSynchronizer = documentSynchronizer;
        }

        protected JoinableTaskFactory JoinableTaskFactory { get; }

        protected ILanguageClientBroker LanguageClientBroker { get; }

        protected LSPDocumentManager DocumentManager { get; }

        protected LSPDocumentSynchronizer DocumentSynchronizer { get; }

        protected virtual Task<TOut> RequestServerAsync<TIn, TOut>(
            ILanguageClientBroker languageClientBroker,
            string method,
            LanguageServerKind serverKind,
            TIn parameters,
            CancellationToken cancellationToken)
        {
            if (languageClientBroker is null)
            {
                throw new ArgumentNullException(nameof(languageClientBroker));
            }

            if (string.IsNullOrEmpty(method))
            {
                throw new ArgumentException("message", nameof(method));
            }

            var contentType = RazorLSPContentTypeDefinition.Name;
            if (serverKind == LanguageServerKind.CSharp)
            {
                contentType = CSharpVirtualDocumentFactory.CSharpLSPContentTypeName;
            }
            else if (serverKind == LanguageServerKind.Html)
            {
                contentType = HtmlVirtualDocumentFactory.HtmlLSPContentTypeName;
            }

            return languageClientBroker.RequestAsync<TIn, TOut>(
                new[] { contentType },
                cap => true,
                method,
                parameters,
                cancellationToken);
        }

        protected virtual async Task<ProjectionResult> GetProjectionAsync(LSPDocumentSnapshot documentSnapshot, Position position, CancellationToken cancellationToken)
        {
            var languageQueryParams = new RazorLanguageQueryParams()
            {
                Position = new OmniSharpPosition(position.Line, position.Character),
                Uri = documentSnapshot.Uri
            };

            var languageResponse = await RequestServerAsync<RazorLanguageQueryParams, RazorLanguageQueryResponse>(
                LanguageClientBroker,
                LanguageServerConstants.RazorLanguageQueryEndpoint,
                LanguageServerKind.Razor,
                languageQueryParams,
                cancellationToken);

            VirtualDocumentSnapshot virtualDocument;
            if (languageResponse.Kind == RazorLanguageKind.CSharp &&
                documentSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var csharpDoc))
            {
                virtualDocument = csharpDoc;
            }
            else if (languageResponse.Kind == RazorLanguageKind.Html &&
                documentSnapshot.TryGetVirtualDocument<HtmlVirtualDocumentSnapshot>(out var htmlDoc))
            {
                virtualDocument = htmlDoc;
            }
            else
            {
                return null;
            }

            if (languageResponse.HostDocumentVersion == UndefinedDocumentVersion)
            {
                // There should always be a document version attached to an open document.
                // TODO: Log it and move on as if it was synchronized.
            }
            else
            {
                var synchronized = await DocumentSynchronizer.TrySynchronizeVirtualDocumentAsync(documentSnapshot, virtualDocument, cancellationToken);
                if (!synchronized)
                {
                    // Could not synchronize
                    return null;
                }
            }

            var result = new ProjectionResult()
            {
                Uri = virtualDocument.Uri,
                Position = new Position((int)languageResponse.Position.Line, (int)languageResponse.Position.Character),
                LanguageKind = languageResponse.Kind,
            };

            return result;
        }
    }
}
