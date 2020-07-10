// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    class RazorComponentPrepareRenameEndpoint : IPrepareRenameHandler
    {
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly ILogger _logger;

        private RenameCapability _capability;

        public RazorComponentPrepareRenameEndpoint(
            ForegroundDispatcher foregroundDispatcher,
            DocumentResolver documentResolver,
            ILoggerFactory loggerFactory)
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _foregroundDispatcher = foregroundDispatcher ?? throw new ArgumentNullException(nameof(foregroundDispatcher));
            _documentResolver = documentResolver ?? throw new ArgumentNullException(nameof(documentResolver));
            _logger = loggerFactory.CreateLogger<RazorComponentRenameEndpoint>();
        }

        public object GetRegistrationOptions()
        {
            return new object();
        }

        public async Task<RangeOrPlaceholderRange> Handle(PrepareRenameParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var document = await Task.Factory.StartNew(() =>
            {
                _documentResolver.TryResolveDocument(request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);
                return documentSnapshot;
            }, cancellationToken, TaskCreationOptions.None, _foregroundDispatcher.ForegroundScheduler).ConfigureAwait(false);

            if (document is null)
            {
                return new Range();
            }

            var codeDocument = await document.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                return new Range();
            }

            if (!FileKinds.IsComponent(codeDocument.GetFileKind()))
            {
                return new Range();
            }

            var sourceText = await document.GetTextAsync().ConfigureAwait(false);
            var linePosition = new LinePosition((int)request.Position.Line, (int)request.Position.Character);
            var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);
            var location = new SourceLocation(hostDocumentIndex, (int)request.Position.Line, (int)request.Position.Character);

            var change = new SourceChange(location.AbsoluteIndex, length: 0, newText: string.Empty);
            var owner = codeDocument.GetSyntaxTree().Root.LocateOwner(change);
            var node = owner.Ancestors().FirstOrDefault(n => n.Kind == SyntaxKind.MarkupTagHelperStartTag);
            if (node == null || !(node is MarkupTagHelperStartTagSyntax tagHelperStartTag))
            {
                return new Range();
            }

            var start = codeDocument.Source.Lines.GetLocation(tagHelperStartTag.Name.Span.Start);
            var end = codeDocument.Source.Lines.GetLocation(tagHelperStartTag.Name.Span.End);

            var range = new Range(
                new Position(start.LineIndex + 1, start.CharacterIndex),
                new Position(end.LineIndex + 1, end.CharacterIndex));
            return new RangeOrPlaceholderRange(range);
        }

        public void SetCapability(RenameCapability capability)
        {
            _capability = capability;
        }
    }
}
 