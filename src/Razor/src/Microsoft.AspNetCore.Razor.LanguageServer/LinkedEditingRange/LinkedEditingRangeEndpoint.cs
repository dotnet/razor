// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Editor.Razor;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using System.Collections.Generic;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.LinkedEditingRange
{
    internal class LinkedEditingRangeEndpoint : ILinkedEditingRangeHandler
    {
        // The regex below excludes characters that can never be valid in a TagHelper name.
        // This is loosely based off logic from the Razor compiler:
        // https://github.com/dotnet/aspnetcore/blob/main/src/Razor/Microsoft.AspNetCore.Razor.Language/src/Legacy/HtmlTokenizer.cs
        // Internal for testing only.
        internal readonly string _wordPattern = @"!?[^ <>!\/\?\[\]=""\\@" + Environment.NewLine + "]+";

        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly TagHelperFactsService _tagHelperFactsService;

        public LinkedEditingRangeEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            TagHelperFactsService tagHelperFactsService)
        {
            if (projectSnapshotManagerDispatcher is null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (documentResolver is null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            if (tagHelperFactsService is null)
            {
                throw new ArgumentNullException(nameof(tagHelperFactsService));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentResolver = documentResolver;
            _tagHelperFactsService = tagHelperFactsService;
        }

        public RegistrationExtensionResult GetRegistration()
        {
            const string AssociatedServerCapability = "linkedEditingRangeProvider";
            var registrationOptions = new LinkedEditingRangeRegistrationOptions();
            return new RegistrationExtensionResult(AssociatedServerCapability, registrationOptions);
        }

        public async Task<LinkedEditingRanges> Handle(
            LinkedEditingRangeParams request,
            CancellationToken cancellationToken)
        {
            var document = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                _documentResolver.TryResolveDocument(
                    request.TextDocument.Uri.GetAbsoluteOrUNCPath(), out var documentSnapshot);

                return documentSnapshot;
            }, cancellationToken);

            if (document is null || cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var codeDocument = await document.GetGeneratedOutputAsync();
            if (codeDocument.IsUnsupported())
            {
                return null;
            }

            var location = await GetSourceLocation(request, document).ConfigureAwait(false);
            var ancestors = GetAncestors(codeDocument, location);

            // We only care if the user is within a TagHelper with a valid start and end tag.
            if (_tagHelperFactsService.TryGetNearestAncestorStartAndEndTags(
                    ancestors, out var startTag, out var endTag) &&
                startTag.Name is not null && endTag.Name is not null)
            {
                var startSpan = startTag.Name.GetLinePositionSpan(codeDocument.Source);
                var endSpan = endTag.Name.GetLinePositionSpan(codeDocument.Source);
                var ranges = new Range[2] { startSpan.ToRange(), endSpan.ToRange() };

                return new LinkedEditingRanges
                {
                    Ranges = ranges,
                    WordPattern = _wordPattern
                };
            }

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

            static IEnumerable<SyntaxNode> GetAncestors(RazorCodeDocument codeDocument, SourceLocation location)
            {
                var syntaxTree = codeDocument.GetSyntaxTree();
                var change = new SourceChange(location.AbsoluteIndex, length: 0, newText: "");
                var owner = syntaxTree.Root.LocateOwner(change);
                var ancestors = owner.Ancestors();

                return ancestors;
            }
        }
    }
}
