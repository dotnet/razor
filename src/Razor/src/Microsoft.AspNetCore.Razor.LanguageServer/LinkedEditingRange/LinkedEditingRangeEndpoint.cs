// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using System.Diagnostics.CodeAnalysis;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer.LinkedEditingRange
{
    internal class LinkedEditingRangeEndpoint : ILinkedEditingRangeHandler
    {
        // The regex below excludes characters that can never be valid in a TagHelper name.
        // This is loosely based off logic from the Razor compiler:
        // https://github.com/dotnet/aspnetcore/blob/9da42b9fab4c61fe46627ac0c6877905ec845d5a/src/Razor/Microsoft.AspNetCore.Razor.Language/src/Legacy/HtmlTokenizer.cs
        // Internal for testing only.
        internal static readonly string WordPattern = @"!?[^ <>!\/\?\[\]=""\\@" + Environment.NewLine + "]+";

        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly ILogger _logger;

        public LinkedEditingRangeEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
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

            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentResolver = documentResolver;
            _logger = loggerFactory.CreateLogger<LinkedEditingRangeEndpoint>();
        }

        public LinkedEditingRangeRegistrationOptions GetRegistrationOptions(
            LinkedEditingRangeClientCapabilities capability,
            ClientCapabilities clientCapabilities)
        {
            return new LinkedEditingRangeRegistrationOptions
            {
                DocumentSelector = RazorDefaults.Selector
            };
        }

#pragma warning disable CS8613 // Nullability of reference types in return type doesn't match implicitly implemented member.
        // The return type of the handler should be nullable. O# tracking issue:
        // https://github.com/OmniSharp/csharp-language-server-protocol/issues/644
        public async Task<LinkedEditingRanges?> Handle(
#pragma warning restore CS8613 // Nullability of reference types in return type doesn't match implicitly implemented member.
            LinkedEditingRangeParams request,
            CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri.GetAbsoluteOrUNCPath();
            var document = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                if (!_documentResolver.TryResolveDocument(uri, out var documentSnapshot))
                {
                    _logger.LogWarning("Unable to resolve document for {Uri}", uri);
                    return null;
                }

                return documentSnapshot;
            }, cancellationToken).ConfigureAwait(false);

            if (document is null || cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Unable to resolve document for {Uri} or cancellation was requested.", uri);
                return null;
            }

            var codeDocument = await document.GetGeneratedOutputAsync();
            if (codeDocument.IsUnsupported())
            {
                _logger.LogWarning("FileKind {FileKind} is unsupported", codeDocument.GetFileKind());
                return null;
            }

            var location = await GetSourceLocation(request, document).ConfigureAwait(false);

            // We only care if the user is within a TagHelper or HTML tag with a valid start and end tag.
            if (TryGetNearestMarkupNameTokens(codeDocument, location, out var startTagNameToken, out var endTagNameToken) &&
                (startTagNameToken.Span.Contains(location.AbsoluteIndex) || endTagNameToken.Span.Contains(location.AbsoluteIndex) ||
                startTagNameToken.Span.End == location.AbsoluteIndex || endTagNameToken.Span.End == location.AbsoluteIndex))
            {
                var startSpan = startTagNameToken.GetLinePositionSpan(codeDocument.Source);
                var endSpan = endTagNameToken.GetLinePositionSpan(codeDocument.Source);
                var ranges = new Range[2] { startSpan.AsRange(), endSpan.AsRange() };

                return new LinkedEditingRanges
                {
                    Ranges = ranges,
                    WordPattern = WordPattern
                };
            }

            _logger.LogInformation("LinkedEditingRange request was null at {location} for {uri}", location, uri);
            return null;

            static async Task<SourceLocation> GetSourceLocation(
                LinkedEditingRangeParams request,
                DocumentSnapshot document)
            {
                var sourceText = await document.GetTextAsync().ConfigureAwait(false);
                var linePosition = new LinePosition(request.Position.Line, request.Position.Character);
                var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);
                var location = new SourceLocation(hostDocumentIndex, request.Position.Line, request.Position.Character);

                return location;
            }

            static bool TryGetNearestMarkupNameTokens(
                RazorCodeDocument codeDocument,
                SourceLocation location,
                [NotNullWhen(true)] out SyntaxToken? startTagNameToken,
                [NotNullWhen(true)] out SyntaxToken? endTagNameToken)
            {
                var syntaxTree = codeDocument.GetSyntaxTree();
                var change = new SourceChange(location.AbsoluteIndex, length: 0, newText: "");
                var owner = syntaxTree.Root.LocateOwner(change);
                var element = owner.FirstAncestorOrSelf<MarkupSyntaxNode>(
                    a => a.Kind is SyntaxKind.MarkupTagHelperElement || a.Kind is SyntaxKind.MarkupElement);

                if (element is null)
                {
                    startTagNameToken = null;
                    endTagNameToken = null;
                    return false;
                }

                switch (element)
                {
                    // Tag helper
                    case MarkupTagHelperElementSyntax markupTagHelperElement:
                        startTagNameToken = markupTagHelperElement.StartTag?.Name;
                        endTagNameToken = markupTagHelperElement.EndTag?.Name;
                        return startTagNameToken is not null && endTagNameToken is not null;
                    // HTML
                    case MarkupElementSyntax markupElement:
                        startTagNameToken = markupElement.StartTag?.Name;
                        endTagNameToken = markupElement.EndTag?.Name;
                        return startTagNameToken is not null && endTagNameToken is not null;
                    default:
                        throw new InvalidOperationException("Element is expected to be a MarkupTagHelperElement or MarkupElement.");
                }
            }
        }
    }
}
