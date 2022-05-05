// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation
{
    internal class TextDocumentTextPresentationEndpoint : AbstractTextDocumentPresentationEndpointBase<TextPresentationParams>
    {
        public TextDocumentTextPresentationEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            RazorDocumentMappingService razorDocumentMappingService,
            ClientNotifierServiceBase languageServer,
            DocumentVersionCache documentVersionCache,
            LanguageServerFeatureOptions languageServerFeatureOptions,
            ILoggerFactory loggerFactory)
            : base(projectSnapshotManagerDispatcher,
                 documentResolver,
                 razorDocumentMappingService,
                 languageServer,
                 documentVersionCache,
                 languageServerFeatureOptions,
                 loggerFactory.CreateLogger<TextDocumentTextPresentationEndpoint>())
        {
        }

        public override string EndpointName => LanguageServerConstants.RazorTextPresentationEndpoint;

        public override RegistrationExtensionResult? GetRegistration(VisualStudio.LanguageServer.Protocol.VSInternalClientCapabilities clientCapabilities)
        {
            const string AssociatedServerCapability = "_vs_textPresentationProvider";

            return new RegistrationExtensionResult(AssociatedServerCapability, options: true);
        }

        protected override IRazorPresentationParams CreateRazorRequestParameters(TextPresentationParams request)
            => new RazorTextPresentationParams(request.TextDocument, request.Range)
            {
                Text = request.Text
            };

        protected override Task<WorkspaceEdit?> TryGetRazorWorkspaceEditAsync(RazorLanguageKind languageKind, TextPresentationParams request, CancellationToken cancellationToken)
        {
            // We don't do anything special with text
            return Task.FromResult<WorkspaceEdit?>(null);
        }
    }
}
