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
            _logger.LogDebug("in handle");
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
                return null;
            }

            var codeDocument = await document.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            if (!FileKinds.IsComponent(codeDocument.GetFileKind()))
            {
                return null;
            }

            var sourceText = await document.GetTextAsync().ConfigureAwait(false);
            var linePosition = new LinePosition((int)request.Position.Line, (int)request.Position.Character);
            var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);
            var location = new SourceLocation(hostDocumentIndex, (int)request.Position.Line, (int)request.Position.Character);

            var change = new SourceChange(location.AbsoluteIndex, length: 0, newText: string.Empty);
            var syntaxTree = codeDocument.GetSyntaxTree();
            if (syntaxTree?.Root is null)
            {
                return null;
            }

            var owner = syntaxTree.Root.LocateOwner(change);
            var node = owner.Ancestors().FirstOrDefault(n => n.Kind == SyntaxKind.MarkupTagHelperStartTag);
            if (node == null || !(node is MarkupTagHelperStartTagSyntax tagHelperStartTag))
            {
                return null;
            }

            _logger.LogDebug($"refactor found {tagHelperStartTag.Name}");
            var start = sourceText.Lines.GetLinePosition(tagHelperStartTag.Name.Span.Start);
            var end = sourceText.Lines.GetLinePosition(tagHelperStartTag.Name.Span.End);
            _logger.LogDebug($"{start} {end}");
            return new RangeOrPlaceholderRange(new Range(new Position(start.Line + 1, start.Character), new Position(start.Line + 1, start.Character)));
        }

        public void SetCapability(RenameCapability capability)
        {
            _capability = capability;
        }
    }
}
 