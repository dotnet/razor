// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    internal class RazorCompletionEndpoint : IVSCompletionEndpoint
    {
        private readonly ILogger _logger;
        private readonly DocumentContextFactory _documentContextFactory;
        private readonly CompletionListProvider _completionListProvider;
        private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;
        private VSInternalClientCapabilities? _clientCapabilities;

        public RazorCompletionEndpoint(
            DocumentContextFactory documentContextFactory,
            CompletionListProvider completionListProvider,
            LanguageServerFeatureOptions languageServerFeatureOptions,
            ILoggerFactory loggerFactory)
        {
            _documentContextFactory = documentContextFactory;
            _completionListProvider = completionListProvider;
            _languageServerFeatureOptions = languageServerFeatureOptions;
            _logger = loggerFactory.CreateLogger<RazorCompletionEndpoint>();
        }

        public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            const string AssociatedServerCapability = "completionProvider";
            _clientCapabilities = clientCapabilities;

            if (!_languageServerFeatureOptions.RegisterBuiltInFeatures)
            {
                return null;
            }

            var registrationOptions = new CompletionOptions()
            {
                ResolveProvider = true,
                TriggerCharacters = _completionListProvider.AggregateTriggerCharacters.ToArray(),
                AllCommitCharacters = new[] { ":", ">", " ", "=" },
            };

            return new RegistrationExtensionResult(AssociatedServerCapability, registrationOptions);
        }

        public async Task<VSInternalCompletionList?> Handle(VSCompletionParamsBridge request, CancellationToken cancellationToken)
        {
            var documentContext = await _documentContextFactory.TryCreateAsync(request.TextDocument.Uri, cancellationToken).ConfigureAwait(false);
            if (documentContext is null)
            {
                return null;
            }

            if (request.Context is null)
            {
                return null;
            }

            var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
            if (!request.Position.TryGetAbsoluteIndex(sourceText, _logger, out var hostDocumentIndex))
            {
                return null;
            }

            if (request.Context is not VSInternalCompletionContext completionContext)
            {
                Debug.Fail("Completion context should never be null in practice");
                return null;
            }

            var completionList = await _completionListProvider.GetCompletionListAsync(
                hostDocumentIndex,
                completionContext,
                documentContext,
                _clientCapabilities!,
                cancellationToken).ConfigureAwait(false);
            return completionList;
        }
    }
}
