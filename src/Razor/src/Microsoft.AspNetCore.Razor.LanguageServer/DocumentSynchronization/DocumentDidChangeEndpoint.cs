// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentSynchronization;

[LanguageServerEndpoint(Methods.TextDocumentDidChangeName)]
internal class DocumentDidChangeEndpoint : IRazorNotificationHandler<DidChangeTextDocumentParams>, ITextDocumentIdentifierHandler<DidChangeTextDocumentParams, TextDocumentIdentifier>, ICapabilitiesProvider
{
    public bool MutatesSolutionState => true;

    private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
    private readonly RazorProjectService _projectService;
    private readonly ProjectSnapshotManagerAccessor _projectSnapshotManagerAccessor;
    private readonly ISnapshotResolver _snapshotResolver;
    private readonly IEnumerable<DocumentProcessedListener> _documentProcessedListeners;

    public DocumentDidChangeEndpoint(
        ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
        RazorProjectService razorProjectService,
        ProjectSnapshotManagerAccessor projectSnapshotManagerAccessor,
        ISnapshotResolver snapshotResolver,
        IEnumerable<DocumentProcessedListener> documentProcessedListeners)
    {
        _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
        _projectService = razorProjectService;
        _projectSnapshotManagerAccessor = projectSnapshotManagerAccessor;
        _snapshotResolver = snapshotResolver;
        _documentProcessedListeners = documentProcessedListeners;
    }

    public void ApplyCapabilities(VSInternalServerCapabilities serverCapabilities, VSInternalClientCapabilities clientCapabilities)
    {
        serverCapabilities.TextDocumentSync = new TextDocumentSyncOptions()
        {
            Change = TextDocumentSyncKind.Incremental,
            OpenClose = true,
            Save = new SaveOptions()
            {
                IncludeText = true,
            },
            WillSave = false,
            WillSaveWaitUntil = false,
        };
    }

    public TextDocumentIdentifier GetTextDocumentIdentifier(DidChangeTextDocumentParams request)
    {
        return request.TextDocument;
    }

    public async Task HandleNotificationAsync(DidChangeTextDocumentParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var documentContext = requestContext.GetRequiredDocumentContext();

        var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
        sourceText = ApplyContentChanges(request.ContentChanges, sourceText, requestContext.Logger);

        await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
            async () =>
            {
                _projectService.UpdateDocument(documentContext.FilePath, sourceText, request.TextDocument.Version);

                if (!_snapshotResolver.TryResolveAllProjects(documentContext.FilePath, out var projects))
                {
                    Assumed.Unreachable();
                }

                foreach (var project in projects)
                {
                    var document = project.GetDocument(documentContext.FilePath).AssumeNotNull();
                    var generatedDocument = await document.GetGeneratedOutputAsync().ConfigureAwait(true);

                    foreach (var listener in _documentProcessedListeners)
                    {
                        listener.DocumentProcessed(generatedDocument, document);
                    }
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    // Internal for testing
    internal SourceText ApplyContentChanges(IEnumerable<TextDocumentContentChangeEvent> contentChanges, SourceText sourceText, ILogger logger)
    {
        foreach (var change in contentChanges)
        {
            if (change.Range is null)
            {
                throw new ArgumentNullException(nameof(change.Range), "Range of change should not be null.");
            }

            var startLinePosition = new LinePosition(change.Range.Start.Line, change.Range.Start.Character);
            var startPosition = sourceText.Lines.GetPosition(startLinePosition);
            var endLinePosition = new LinePosition(change.Range.End.Line, change.Range.End.Character);
            var endPosition = sourceText.Lines.GetPosition(endLinePosition);

            var textSpan = new TextSpan(startPosition, change.RangeLength ?? endPosition - startPosition);
            var textChange = new TextChange(textSpan, change.Text);

            logger.LogInformation("Applying {textChange}", textChange);

            // If there happens to be multiple text changes we generate a new source text for each one. Due to the
            // differences in VSCode and Roslyn's representation we can't pass in all changes simultaneously because
            // ordering may differ.
            sourceText = sourceText.WithChanges(textChange);
        }

        return sourceText;
    }
}
