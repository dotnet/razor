// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using System.Threading;
using LS = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.SignatureHelp
{
    internal class SignatureHelpEndpoint : AbstractRazorDelegatingEndpoint<SignatureHelpParamsBridge, LS.SignatureHelp>
    {
        public SignatureHelpEndpoint(
            DocumentContextFactory documentContextFactory,
            LanguageServerFeatureOptions languageServerFeatureOptions,
            RazorDocumentMappingService documentMappingService,
            ClientNotifierServiceBase languageServer,
            ILoggerFactory loggerProvider)
            : base(documentContextFactory, languageServerFeatureOptions, documentMappingService, languageServer, loggerProvider.CreateLogger<SignatureHelpEndpoint>())
        {

        }

        /// <inheritdoc />
        protected override string CustomMessageTarget => RazorLanguageServerCustomMessageTargets.RazorSignatureHelpEndpointName;

        /// <inheritdoc />
        protected override IDelegatedParams CreateDelegatedParams(SignatureHelpParamsBridge request, DocumentContext documentContext, Projection projection, CancellationToken cancellationToken)
            => new DelegatedPositionParams(
                    documentContext.Identifier,
                    projection.Position,
                    projection.LanguageKind);
    }
}
