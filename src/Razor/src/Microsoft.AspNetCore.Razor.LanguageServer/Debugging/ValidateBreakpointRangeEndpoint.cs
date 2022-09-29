// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Debugging
{
    internal class ValidateBreakpointRangeEndpoint : AbstractRazorDelegatingEndpoint<ValidateBreakpointRangeParamsBridge, Range?>, IValidateBreakpointRangeEndpoint
    {
        private readonly RazorDocumentMappingService _documentMappingService;

        public ValidateBreakpointRangeEndpoint(
            DocumentContextFactory documentContextFactory,
            RazorDocumentMappingService documentMappingService,
            LanguageServerFeatureOptions languageServerFeatureOptions,
            ClientNotifierServiceBase languageServer,
            ILoggerFactory loggerFactory)
            : base(documentContextFactory, languageServerFeatureOptions, documentMappingService, languageServer, loggerFactory.CreateLogger<ValidateBreakpointRangeEndpoint>())
        {
            _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
        }

        protected override bool OnlySingleServer => false;

        protected override string CustomMessageTarget => RazorLanguageServerCustomMessageTargets.RazorValidateBreakpointRangeName;

        public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            const string ServerCapability = "_vs_breakableRangeProvider";

            return new RegistrationExtensionResult(ServerCapability, true);
        }

        protected override Task<Range?> TryHandleAsync(ValidateBreakpointRangeParamsBridge request, DocumentContext documentContext, Projection projection, CancellationToken cancellationToken)
        {
            // no such thing as Razor breakpoints (yet?!)
            return Task.FromResult<Range?>(null);
        }

        protected async override Task<IDelegatedParams?> CreateDelegatedParamsAsync(ValidateBreakpointRangeParamsBridge request, DocumentContext documentContext, Projection projection, CancellationToken cancellationToken)
        {
            // only C# supports breakpoints
            if (projection.LanguageKind != RazorLanguageKind.CSharp)
            {
                return null;
            }

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

            if (!_documentMappingService.TryMapToProjectedDocumentRange(codeDocument, request.Range, out var projectedRange))
            {
                return null;
            }

            return new DelegatedValidateBreakpointRangeParams(
                documentContext.Identifier,
                projectedRange,
                projection.LanguageKind);
        }

        protected async override Task<Range?> HandleDelegatedResponseAsync(Range? response, ValidateBreakpointRangeParamsBridge originalRequest, DocumentContext documentContext, Projection projection, CancellationToken cancellationToken)
        {
            if (response is null)
            {
                return null;
            }

            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);

            if (_documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, response, out var projectedRange))
            {
                return projectedRange;
            }

            return null;
        }
    }
}
