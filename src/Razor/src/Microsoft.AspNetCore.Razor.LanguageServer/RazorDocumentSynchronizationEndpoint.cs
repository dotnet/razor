// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class RazorDocumentSynchronizationEndpoint : IVSTextDocumentSyncHandler
    {
        private readonly ILogger _logger;
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly RazorProjectService _projectService;

        public RazorDocumentSynchronizationEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            RazorProjectService projectService,
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

            if (projectService is null)
            {
                throw new ArgumentNullException(nameof(projectService));
            }

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentResolver = documentResolver;
            _projectService = projectService;
            _logger = loggerFactory.CreateLogger<RazorDocumentSynchronizationEndpoint>();
        }

        public async Task<Unit> Handle(DidChangeTextDocumentParamsBridge notification, CancellationToken token)
        {
            var uri = notification.TextDocument.Uri.GetAbsoluteOrUNCPath();
            var document = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                _documentResolver.TryResolveDocument(uri, out var documentSnapshot);

                return documentSnapshot;
            }, CancellationToken.None).ConfigureAwait(false);

            if (document is null)
            {
                throw new InvalidOperationException(RazorLS.Resources.FormatDocument_Not_Found(uri));
            }

            var sourceText = await document.GetTextAsync();
            sourceText = ApplyContentChanges(notification.ContentChanges, sourceText);

            await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                () => _projectService.UpdateDocument(document.FilePath, sourceText, notification.TextDocument.Version),
                CancellationToken.None).ConfigureAwait(false);

            return Unit.Value;
        }

        public async Task<Unit> Handle(DidOpenTextDocumentParamsBridge notification, CancellationToken token)
        {
            var sourceText = SourceText.From(notification.TextDocument.Text);

            await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                () => _projectService.OpenDocument(notification.TextDocument.Uri.GetAbsoluteOrUNCPath(), sourceText, notification.TextDocument.Version),
                CancellationToken.None).ConfigureAwait(false);

            return Unit.Value;
        }

        public async Task<Unit> Handle(DidCloseTextDocumentParamsBridge notification, CancellationToken token)
        {
            await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(
                () => _projectService.CloseDocument(notification.TextDocument.Uri.GetAbsoluteOrUNCPath()),
                token).ConfigureAwait(false);

            return Unit.Value;
        }

        public Task<Unit> Handle(DidSaveTextDocumentParamsBridge notification, CancellationToken _)
        {
            _logger.LogInformation($"Saved Document {notification.TextDocument.Uri.GetAbsoluteOrUNCPath()}");

            return Unit.Task;
        }

        // Internal for testing
        internal SourceText ApplyContentChanges(IEnumerable<TextDocumentContentChangeEvent> contentChanges, SourceText sourceText)
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

                _logger.LogTrace($"Applying {textChange}");

                // If there happens to be multiple text changes we generate a new source text for each one. Due to the
                // differences in VSCode and Roslyn's representation we can't pass in all changes simultaneously because
                // ordering may differ.
                sourceText = sourceText.WithChanges(textChange);
            }

            return sourceText;
        }

        public RegistrationExtensionResult? GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            const string AssociatedServerCapability = "textDocumentSync";
            var registrationOptions = new TextDocumentSyncOptions()
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

            var result = new RegistrationExtensionResult(AssociatedServerCapability, registrationOptions);

            return result;
        }
    }
}
