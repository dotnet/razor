// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;
using Microsoft.VisualStudio.Text;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [Shared]
    [ExportLspMethod(Methods.TextDocumentOnTypeFormattingName)]
    internal class OnTypeFormattingHandler : IRequestHandler<DocumentOnTypeFormattingParams, TextEdit[]>
    {
        private static readonly IReadOnlyList<string> s_csharpTriggerCharacters = new[] { "}", ";" };
        private static readonly IReadOnlyList<string> s_htmlTriggerCharacters = Array.Empty<string>();
        private static readonly IReadOnlyList<string> s_allTriggerCharacters = s_csharpTriggerCharacters.Concat(s_htmlTriggerCharacters).ToArray();

        private readonly LSPDocumentManager _documentManager;
        private readonly LSPDocumentSynchronizer _documentSynchronizer;
        private readonly LSPProjectionProvider _projectionProvider;
        private readonly LSPDocumentMappingProvider _documentMappingProvider;
        private readonly VSHostServicesProvider _vsHostServicesProvider;
        private readonly ILogger _logger;

        [ImportingConstructor]
        public OnTypeFormattingHandler(
            LSPDocumentManager documentManager,
            LSPDocumentSynchronizer documentSynchronizer,
            LSPProjectionProvider projectionProvider,
            LSPDocumentMappingProvider documentMappingProvider,
            VSHostServicesProvider vsHostServicesProvider,
            HTMLCSharpLanguageServerLogHubLoggerProvider loggerProvider)
        {
            if (documentManager is null)
            {
                throw new ArgumentNullException(nameof(documentManager));
            }

            if (documentSynchronizer is null)
            {
                throw new ArgumentNullException(nameof(documentSynchronizer));
            }

            if (projectionProvider is null)
            {
                throw new ArgumentNullException(nameof(projectionProvider));
            }

            if (documentMappingProvider is null)
            {
                throw new ArgumentNullException(nameof(documentMappingProvider));
            }

            if (vsHostServicesProvider is null)
            {
                throw new ArgumentNullException(nameof(vsHostServicesProvider));
            }

            if (loggerProvider is null)
            {
                throw new ArgumentNullException(nameof(loggerProvider));
            }

            _documentManager = documentManager;
            _documentSynchronizer = documentSynchronizer;
            _projectionProvider = projectionProvider;
            _documentMappingProvider = documentMappingProvider;
            _vsHostServicesProvider = vsHostServicesProvider;

            _logger = loggerProvider.CreateLogger(nameof(OnTypeFormattingHandler));
        }

        public async Task<TextEdit[]> HandleRequestAsync(
            DocumentOnTypeFormattingParams request,
            ClientCapabilities clientCapabilities,
            CancellationToken cancellationToken)
        {
            if (!s_allTriggerCharacters.Contains(request.Character, StringComparer.Ordinal))
            {
                // Unexpected trigger character.
                return null;
            }

            _logger.LogInformation($"Starting request for {request.TextDocument.Uri}.");

            if (!_documentManager.TryGetDocument(request.TextDocument.Uri, out var documentSnapshot))
            {
                _logger.LogWarning($"Failed to find document {request.TextDocument.Uri}.");
                return null;
            }

            var triggerCharacterKind = await GetTriggerCharacterLanguageKindAsync(
                documentSnapshot, request.Position, request.Character, cancellationToken).ConfigureAwait(false);
            if (triggerCharacterKind is null)
            {
                _logger.LogInformation($"Failed to identify trigger character language context.");
                return null;
            }
            else if (triggerCharacterKind is not RazorLanguageKind.CSharp)
            {
                _logger.LogInformation($"Unsupported trigger character language {triggerCharacterKind:G}.");
                return null;
            }

            if (!IsApplicableTriggerCharacter(request.Character, triggerCharacterKind.Value))
            {
                // We were triggered but the trigger character doesn't make sense for the current cursor position. Bail.
                _logger.LogInformation("Unsupported trigger character location.");
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var projectionResult = await _projectionProvider.GetProjectionAsync(
                documentSnapshot,
                request.Position,
                cancellationToken).ConfigureAwait(false);
            if (projectionResult is null)
            {
                return null;
            }

            // There's a chance that the virtual document is not up to date. This can occur if the virtual document
            // was not synchronized at the time TryGetDocument was called. To circumvent this, we wait until we have
            // proper synchronization before proceeding.
            var virtualDocument = await GetSynchronizedVirtualDocumentAsync(
                request.TextDocument.Uri, documentSnapshot, cancellationToken).ConfigureAwait(false);
            if (virtualDocument is null)
            {
                return null;
            }

            var csharpSourceText = SourceText.From(virtualDocument.Snapshot.GetText());
            var document = GenerateRoslynCSharpDocument(csharpSourceText, _vsHostServicesProvider);
            var documentOptions = await GetDocumentOptionsAsync(request, document).ConfigureAwait(false);

            // We call into Roslyn's formatting service directly via external access, which circumvents needing
            // to send a request to the C# LSP server.
            var formattingChanges = await RazorCSharpFormattingInteractionService.GetFormattingChangesAsync(
                document, typedChar: request.Character[0], projectionResult.PositionIndex, documentOptions,
                cancellationToken).ConfigureAwait(false);

            document.Project.Solution.Workspace.Dispose();

            if (formattingChanges.IsEmpty)
            {
                _logger.LogInformation("Received no results.");
                return null;
            }

            var textEdits = formattingChanges.Select(
                change => Extensions.TextChangeExtensions.AsTextEdit(change, csharpSourceText)).ToArray();
            _logger.LogInformation($"Received {textEdits.Length} results, remapping.");

            cancellationToken.ThrowIfCancellationRequested();

            var remappedTextEdits = await _documentMappingProvider.RemapFormattedTextEditsAsync(
                projectionResult.Uri, textEdits, request.Options, containsSnippet: false, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation($"Returning {remappedTextEdits?.Length} text edits.");
            return remappedTextEdits;
        }

        private async Task<RazorLanguageKind?> GetTriggerCharacterLanguageKindAsync(
            LSPDocumentSnapshot documentSnapshot,
            Position positionAfterTriggerChar,
            string triggerCharacter,
            CancellationToken cancellationToken)
        {
            // request.Character will point to the position after the character that was inserted.
            // For onTypeFormatting, it makes more sense to look up the projection of the character that was inserted.
            var line = documentSnapshot.Snapshot.GetLineFromLineNumber(positionAfterTriggerChar.Line);
            var position = line.Start.Position + positionAfterTriggerChar.Character;
            var point = new SnapshotPoint(documentSnapshot.Snapshot, position);

            // Subtract the trigger character length to go back to the position of the trigger character
            var triggerCharacterPoint = point.Subtract(triggerCharacter.Length);

            var triggerCharacterLine = documentSnapshot.Snapshot.GetLineFromPosition(triggerCharacterPoint.Position);
            var triggerCharacterPosition = new Position(
                triggerCharacterLine.LineNumber, triggerCharacterPoint.Position - triggerCharacterLine.Start.Position);

            var triggerCharacterProjectionResult = await _projectionProvider.GetProjectionAsync(
                documentSnapshot, triggerCharacterPosition, cancellationToken).ConfigureAwait(false);

            return triggerCharacterProjectionResult?.LanguageKind;
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

        private async Task<CSharpVirtualDocumentSnapshot> GetSynchronizedVirtualDocumentAsync(
            Uri razorDocUri,
            LSPDocumentSnapshot documentSnapshot,
            CancellationToken cancellationToken)
        {
            if (!documentSnapshot.TryGetVirtualDocument(out CSharpVirtualDocumentSnapshot virtualDocumentSnapshot))
            {
                return null;
            }

            var synchronized = await _documentSynchronizer.TrySynchronizeVirtualDocumentAsync(
                documentSnapshot.Version, virtualDocumentSnapshot, cancellationToken).ConfigureAwait(false);

            LSPDocumentSnapshot updatedSnapshot = null;
            if (synchronized && _documentManager.TryGetDocument(razorDocUri, out updatedSnapshot))
            {
                if (!updatedSnapshot.TryGetVirtualDocument<CSharpVirtualDocumentSnapshot>(out var updatedVirtualSnapshot))
                {
                    return null;
                }

                return updatedVirtualSnapshot;
            }

            // Recursively call this method until we reach a synchronized state. We need to be synchronized
            // to ensure our C# virtual document is up to date.
            return await GetSynchronizedVirtualDocumentAsync(razorDocUri, updatedSnapshot, cancellationToken).ConfigureAwait(false);
        }

        // Internal for testing
        internal static Document GenerateRoslynCSharpDocument(SourceText csharpSourceText, VSHostServicesProvider hostServicesProvider)
        {
            // We dispose of the workspace in the caller.
#pragma warning disable CA2000 // Dispose objects before losing scope
            var workspace = new AdhocWorkspace(hostServicesProvider.GetServices());
#pragma warning restore CA2000 // Dispose objects before losing scope

            var project = workspace.AddProject("TestProject", LanguageNames.CSharp);
            var document = workspace.AddDocument(project.Id, "TestDocument", csharpSourceText);
            return document;
        }

        // Internal for testing
        internal static async Task<DocumentOptionSet> GetDocumentOptionsAsync(DocumentOnTypeFormattingParams request, Document document)
        {
            var documentOptions = await document.GetOptionsAsync().ConfigureAwait(false);
            documentOptions = documentOptions.WithChangedOption(CodeAnalysis.Formatting.FormattingOptions.TabSize, request.Options.TabSize)
                .WithChangedOption(CodeAnalysis.Formatting.FormattingOptions.IndentationSize, request.Options.TabSize)
                .WithChangedOption(CodeAnalysis.Formatting.FormattingOptions.UseTabs, !request.Options.InsertSpaces);
            return documentOptions;
        }
    }
}
