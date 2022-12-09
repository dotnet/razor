// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.FindAllReferences
{
    internal class FindAllReferencesEndpoint : AbstractRazorDelegatingEndpoint<ReferenceParamsBridge, VSInternalReferenceItem[]>, IVSFindAllReferencesEndpoint
    {
        private VSInternalClientCapabilities? _clientCapabilities;

        public FindAllReferencesEndpoint(
            LanguageServerFeatureOptions languageServerFeatureOptions,
            RazorDocumentMappingService documentMappingService,
            ClientNotifierServiceBase languageServer,
            ILoggerFactory loggerFactory)
            : base(languageServerFeatureOptions, documentMappingService, languageServer, loggerFactory.CreateLogger<FindAllReferencesEndpoint>())
        {
        }

        public RegistrationExtensionResult GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            const string AssociatedServerCapability = "referencesProvider";
            _clientCapabilities = clientCapabilities;

            var registrationOptions = new ReferenceOptions()
            {
                WorkDoneProgress = true,
            };

            return new RegistrationExtensionResult(AssociatedServerCapability, new SumType<bool, ReferenceOptions>(registrationOptions));
        }

        protected override string CustomMessageTarget => RazorLanguageServerCustomMessageTargets.RazorReferencesEndpointName;

        protected override Task<IDelegatedParams?> CreateDelegatedParamsAsync(ReferenceParamsBridge request, RazorRequestContext requestContext, Projection projection, CancellationToken cancellationToken)
        {
            var documentContext = requestContext.GetRequiredDocumentContext();
            return Task.FromResult<IDelegatedParams?>(new DelegatedPositionParams(
                    documentContext.Identifier,
                    projection.Position,
                    projection.LanguageKind));
        }

        protected override Task<VSInternalReferenceItem[]?> TryHandleAsync(ReferenceParamsBridge request, RazorRequestContext requestContext, Projection projection, CancellationToken cancellationToken)
        {
            return base.TryHandleAsync(request, requestContext, projection, cancellationToken);
        }
    }
}
