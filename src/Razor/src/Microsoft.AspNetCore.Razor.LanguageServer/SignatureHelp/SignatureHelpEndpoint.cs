﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using LS = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.SignatureHelp
{
    internal class SignatureHelpEndpoint : AbstractRazorDelegatingEndpoint<SignatureHelpParamsBridge, LS.SignatureHelp>, ISignatureHelpEndpoint
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

        public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            const string ServerCapability = "signatureHelpProvider";
            var option = new SignatureHelpOptions()
            {
                TriggerCharacters = new[] { "(", ",", "<" },
                RetriggerCharacters = new[] { ">", ")" }
            };

            return new RegistrationExtensionResult(ServerCapability, option);
        }

        /// <inheritdoc />
        protected override IDelegatedParams CreateDelegatedParams(SignatureHelpParamsBridge request, DocumentContext documentContext, Projection projection, CancellationToken cancellationToken)
            => new DelegatedPositionParams(
                    documentContext.Identifier,
                    projection.Position,
                    projection.LanguageKind);
    }
}
