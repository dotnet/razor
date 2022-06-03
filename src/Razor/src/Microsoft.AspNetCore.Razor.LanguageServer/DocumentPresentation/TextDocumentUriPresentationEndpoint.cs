﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation
{
    internal class TextDocumentUriPresentationEndpoint : AbstractTextDocumentPresentationEndpointBase<UriPresentationParams>, ITextDocumentUriPresentationHandler
    {
        private readonly RazorComponentSearchEngine _razorComponentSearchEngine;

        public TextDocumentUriPresentationEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            RazorDocumentMappingService razorDocumentMappingService,
            RazorComponentSearchEngine razorComponentSearchEngine,
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
                 loggerFactory.CreateLogger<TextDocumentUriPresentationEndpoint>())
        {
            if (razorComponentSearchEngine is null)
            {
                throw new ArgumentNullException(nameof(razorComponentSearchEngine));
            }

            _razorComponentSearchEngine = razorComponentSearchEngine;
        }

        public override string EndpointName => LanguageServerConstants.RazorUriPresentationEndpoint;

        public override RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            const string AssociatedServerCapability = "_vs_uriPresentationProvider";

            return new RegistrationExtensionResult(AssociatedServerCapability, options: true);
        }

        protected override IRazorPresentationParams CreateRazorRequestParameters(UriPresentationParams request)
            => new RazorUriPresentationParams()
            {
                TextDocument = request.TextDocument,
                Range = request.Range,
                Uris = request.Uris
            };

        protected override async Task<WorkspaceEdit?> TryGetRazorWorkspaceEditAsync(RazorLanguageKind languageKind, UriPresentationParams request, CancellationToken cancellationToken)
        {
            if (languageKind is not RazorLanguageKind.Html)
            {
                // We don't do anything for HTML
                return null;
            }

            if (request.Uris is null || request.Uris.Length == 0)
            {
                _logger.LogInformation($"No URIs were included in the request?");
                return null;
            }

            // We only want to handle requests for a single .razor file, but when there are files nested under a .razor
            // file (for example, Goo.razor.css, Goo.razor.cs etc.) then we'll get all of those files as well, when the user
            // thinks they're just dragging the parent one, so we have to be a little bit clever with the filter here
            var razorFileUri = request.Uris.Last();
            var fileName = Path.GetFileName(razorFileUri.GetAbsoluteOrUNCPath());
            if (!fileName.EndsWith(".razor", FilePathComparison.Instance))
            {
                _logger.LogInformation("Last file in the drop was not a single razor file URI.");
                return null;
            }

            if (request.Uris.Any(uri => !Path.GetFileName(uri.GetAbsoluteOrUNCPath()).StartsWith(fileName, FilePathComparison.Instance)))
            {
                _logger.LogInformation("One or more URIs were not a child file of the main .razor file.");
                return null;
            }

            var componentTagText = await TryGetComponentTagAsync(razorFileUri, cancellationToken).ConfigureAwait(false);
            if (componentTagText is null)
            {
                return null;
            }

            return new WorkspaceEdit
            {
                DocumentChanges = new TextDocumentEdit[]
                {
                    new TextDocumentEdit
                    {
                        TextDocument = new()
                        {
                            Uri = request.TextDocument.Uri
                        },
                        Edits = new[]
                        {
                            new TextEdit
                            {
                                NewText = componentTagText,
                                Range = request.Range
                            }
                        }
                    }
                }
            };
        }

        private async Task<string?> TryGetComponentTagAsync(Uri uri, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Trying to find document info for dropped uri {uri}.");

            var documentSnapshot = await TryGetDocumentSnapshotAsync(uri.GetAbsoluteOrUNCPath(), cancellationToken).ConfigureAwait(false);
            if (documentSnapshot is null)
            {
                _logger.LogInformation($"Failed to find document for component {uri}.");
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var descriptor = await _razorComponentSearchEngine.TryGetTagHelperDescriptorAsync(documentSnapshot, cancellationToken).ConfigureAwait(false);
            if (descriptor is null)
            {
                _logger.LogInformation($"Failed to find tag helper descriptor.");
                return null;
            }

            var typeName = descriptor.GetTypeNameIdentifier();
            if (string.IsNullOrWhiteSpace(typeName))
            {
                _logger.LogWarning($"Found a tag helper, {descriptor.Name}, but it has an empty TypeNameIdentifier.");
                return null;
            }

            // TODO: Add @using statements if required, or fully qualify (GetTypeName())

            var sb = new StringBuilder();
            sb.Append('<');
            sb.Append(typeName);

            foreach (var requiredAttribute in descriptor.EditorRequiredAttributes)
            {
                sb.Append(' ');
                sb.Append(requiredAttribute.Name);
                sb.Append("=\"\"");
            }

            if (descriptor.AllowedChildTags.Count > 0)
            {
                sb.Append("></");
                sb.Append(typeName);
                sb.Append('>');
            }
            else
            {
                sb.Append(" />");
            }

            return sb.ToString();
        }
    }
}
