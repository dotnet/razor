// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation
{
    internal class TextDocumentUriPresentationEndpoint : ITextDocumentUriPresentationHandler
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly RazorDocumentMappingService _razorDocumentMappingService;
        private readonly RazorComponentSearchEngine _razorComponentSearchEngine;
        private readonly ILogger _logger;

        public TextDocumentUriPresentationEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            RazorDocumentMappingService razorDocumentMappingService,
            RazorComponentSearchEngine razorComponentSearchEngine,
            ILoggerFactory loggerFactory)
        {
            if (projectSnapshotManagerDispatcher is null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (documentResolver is null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            if (razorDocumentMappingService is null)
            {
                throw new ArgumentNullException(nameof(razorDocumentMappingService));
            }

            if (razorComponentSearchEngine is null)
            {
                throw new ArgumentNullException(nameof(razorComponentSearchEngine));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentResolver = documentResolver;
            _razorDocumentMappingService = razorDocumentMappingService;
            _razorComponentSearchEngine = razorComponentSearchEngine;
            _logger = loggerFactory.CreateLogger<TextDocumentUriPresentationEndpoint>();
        }

        public RegistrationExtensionResult? GetRegistration(VisualStudio.LanguageServer.Protocol.VSInternalClientCapabilities clientCapabilities)
        {
            const string AssociatedServerCapability = "_vs_uriPresentationProvider";

            return new RegistrationExtensionResult(AssociatedServerCapability, true);
        }

        public async Task<WorkspaceEdit?> Handle(UriPresentationParams request, CancellationToken cancellationToken)
        {
            var documentSnapshot = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                _documentResolver.TryResolveDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);

                return documentSnapshot;
            }, cancellationToken).ConfigureAwait(false);

            if (documentSnapshot is null)
            {
                _logger.LogWarning($"Failed to find document {request.TextDocument.Uri}.");
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                _logger.LogWarning($"Failed to retrieve generated output for document {request.TextDocument.Uri}.");
                return null;
            }

            var sourceText = await documentSnapshot.GetTextAsync().ConfigureAwait(false);
            if (request.Range?.Start.TryGetAbsoluteIndex(sourceText, _logger, out var hostDocumentIndex) != true)
            {
                return null;
            }

            var languageKind = _razorDocumentMappingService.GetLanguageKind(codeDocument, hostDocumentIndex);
            if (languageKind is not RazorLanguageKind.Html)
            {
                _logger.LogInformation($"Unsupported language {languageKind:G}.");
                return null;
            }

            if (request.Uris is null || request.Uris.Length == 0)
            {
                _logger.LogInformation($"Didn't get any Uris?");
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
                DocumentChanges = new Container<WorkspaceEditDocumentChange>(
                    new WorkspaceEditDocumentChange(
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
                    )
                )
            };
        }

        private async Task<string?> TryGetComponentTagAsync(Uri uri, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Trying to find document info for dropped uri {uri}.");

            var documentSnapshot = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                _documentResolver.TryResolveDocument(uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);

                return documentSnapshot;
            }, cancellationToken).ConfigureAwait(false);

            if (documentSnapshot is null)
            {
                _logger.LogWarning($"Failed to find document {uri}.");
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var descriptor = await _razorComponentSearchEngine.TryGetTagHelperDescriptorAsync(documentSnapshot, cancellationToken).ConfigureAwait(false);
            if (descriptor is null)
            {
                _logger.LogWarning($"Failed to find tag helper descriptor.");
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
