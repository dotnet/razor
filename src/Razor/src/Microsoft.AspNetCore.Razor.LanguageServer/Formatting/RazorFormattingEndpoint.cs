// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    internal class RazorFormattingEndpoint : IDocumentFormattingHandler, IDocumentRangeFormattingHandler, IDocumentOnTypeFormattingHandler
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly RazorFormattingService _razorFormattingService;
        private readonly RazorDocumentMappingService _razorDocumentMappingService;
        private readonly IOptionsMonitor<RazorLSPOptions> _optionsMonitor;
        private readonly ILogger _logger;

        private static readonly IReadOnlyList<string> s_csharpTriggerCharacters = new[] { "}", ";" };
        private static readonly IReadOnlyList<string> s_htmlTriggerCharacters = new[] { "\n", "{", "}", ";" };
        private static readonly IReadOnlyList<string> s_allTriggerCharacters = s_csharpTriggerCharacters.Concat(s_htmlTriggerCharacters).ToArray();

        public RazorFormattingEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher!!,
            DocumentResolver documentResolver!!,
            RazorFormattingService razorFormattingService!!,
            RazorDocumentMappingService razorDocumentMappingService!!,
            IOptionsMonitor<RazorLSPOptions> optionsMonitor!!,
            ILoggerFactory loggerFactory!!)
        {
            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentResolver = documentResolver;
            _razorFormattingService = razorFormattingService;
            _razorDocumentMappingService = razorDocumentMappingService;
            _optionsMonitor = optionsMonitor;
            _logger = loggerFactory.CreateLogger<RazorFormattingEndpoint>();
        }

        public DocumentFormattingRegistrationOptions GetRegistrationOptions(DocumentFormattingCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DocumentFormattingRegistrationOptions
            {
                DocumentSelector = RazorDefaults.Selector,
            };
        }

        public DocumentRangeFormattingRegistrationOptions GetRegistrationOptions(DocumentRangeFormattingCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DocumentRangeFormattingRegistrationOptions
            {
                DocumentSelector = RazorDefaults.Selector,
            };
        }

        public DocumentOnTypeFormattingRegistrationOptions GetRegistrationOptions(DocumentOnTypeFormattingCapability capability, ClientCapabilities clientCapabilities)
        {
            Assumes.NotNullOrEmpty(s_allTriggerCharacters);

            return new DocumentOnTypeFormattingRegistrationOptions
            {
                DocumentSelector = RazorDefaults.Selector,
                FirstTriggerCharacter = s_allTriggerCharacters[0],
                MoreTriggerCharacter = s_allTriggerCharacters.Skip(1).ToArray(),
            };
        }

        public async Task<TextEditContainer?> Handle(DocumentFormattingParams request, CancellationToken cancellationToken)
        {
            if (!_optionsMonitor.CurrentValue.EnableFormatting)
            {
                return null;
            }

            var document = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                _documentResolver.TryResolveDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);

                return documentSnapshot;
            }, cancellationToken).ConfigureAwait(false);

            if (document is null || cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var codeDocument = await document.GetGeneratedOutputAsync();
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            var span = TextSpan.FromBounds(0, codeDocument.Source.Length);
            var range = span.AsRange(codeDocument.GetSourceText());
            var edits = await _razorFormattingService.FormatAsync(request.TextDocument.Uri, document, range, request.Options, cancellationToken);

            var editContainer = new TextEditContainer(edits);
            return editContainer;
        }

#nullable disable // OmniSharp annotations don't allow a null return, though the spec does
        public async Task<TextEditContainer> Handle(DocumentRangeFormattingParams request, CancellationToken cancellationToken)
        {
            if (!_optionsMonitor.CurrentValue.EnableFormatting)
            {
                return null;
            }

            var document = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                _documentResolver.TryResolveDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);

                return documentSnapshot;
            }, cancellationToken).ConfigureAwait(false);

            if (document is null || cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var codeDocument = await document.GetGeneratedOutputAsync();
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            var edits = await _razorFormattingService.FormatAsync(request.TextDocument.Uri, document, request.Range, request.Options, cancellationToken);

            var editContainer = new TextEditContainer(edits);
            return editContainer;
        }
#nullable restore

        public async Task<TextEditContainer?> Handle(DocumentOnTypeFormattingParams request, CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Starting OnTypeFormatting request for {request.TextDocument.Uri}.");

            if (!_optionsMonitor.CurrentValue.EnableFormatting)
            {
                _logger.LogInformation("Formatting option disabled.");
                return null;
            }

            if (!s_allTriggerCharacters.Contains(request.Character, StringComparer.Ordinal))
            {
                _logger.LogWarning($"Unexpected trigger character '{request.Character}'.");
                return null;
            }

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

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync();
            if (codeDocument.IsUnsupported())
            {
                _logger.LogWarning($"Failed to retrieve generated output for document {request.TextDocument.Uri}.");
                return null;
            }

            var sourceText = await documentSnapshot.GetTextAsync().ConfigureAwait(false);
            if (!request.Position.TryGetAbsoluteIndex(sourceText, _logger, out var hostDocumentIndex))
            {
                return null;
            }

            var triggerCharacterKind = _razorDocumentMappingService.GetLanguageKind(codeDocument, hostDocumentIndex);
            if (triggerCharacterKind is not (RazorLanguageKind.CSharp or RazorLanguageKind.Html))
            {
                _logger.LogInformation($"Unsupported trigger character language {triggerCharacterKind:G}.");
                return null;
            }

            if (!IsApplicableTriggerCharacter(request.Character, triggerCharacterKind))
            {
                // We were triggered but the trigger character doesn't make sense for the current cursor position. Bail.
                _logger.LogInformation("Unsupported trigger character location.");
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            Debug.Assert(request.Character.Length > 0);

            var formattedEdits = await _razorFormattingService.FormatOnTypeAsync(request.TextDocument.Uri, documentSnapshot, triggerCharacterKind, Array.Empty<TextEdit>(), request.Options, hostDocumentIndex, request.Character[0], cancellationToken).ConfigureAwait(false);
            if (formattedEdits.Length == 0)
            {
                _logger.LogInformation("No formatting changes were necessary");
                return null;
            }

            _logger.LogInformation($"Returning {formattedEdits.Length} final formatted results.");
            return formattedEdits;
        }

        private static bool IsApplicableTriggerCharacter(string triggerCharacter, RazorLanguageKind languageKind)
        {
            if (languageKind == RazorLanguageKind.CSharp)
            {
                return s_csharpTriggerCharacters.Contains(triggerCharacter);
            }
            else if (languageKind == RazorLanguageKind.Html)
            {
                return s_htmlTriggerCharacters.Contains(triggerCharacter);
            }

            // Unknown trigger character.
            return false;
        }
    }
}
