// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Definition
{
    internal class RazorDefinitionEndpoint : IDefinitionHandler
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly RazorComponentSearchEngine _componentSearchEngine;
        private readonly ILogger<RazorDefinitionEndpoint> _logger;

        public RazorDefinitionEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            RazorComponentSearchEngine componentSearchEngine,
            ILoggerFactory loggerFactory)
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher ?? throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            _documentResolver = documentResolver ?? throw new ArgumentNullException(nameof(documentResolver));
            _componentSearchEngine = componentSearchEngine ?? throw new ArgumentNullException(nameof(componentSearchEngine));
            _logger = loggerFactory.CreateLogger<RazorDefinitionEndpoint>();
        }

        public DefinitionRegistrationOptions GetRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DefinitionRegistrationOptions
            {
                DocumentSelector = RazorDefaults.Selector,
            };
        }

#pragma warning disable CS8613 // Nullability of reference types in return type doesn't match implicitly implemented member.
        // The return type of the handler should be nullable. O# tracking issue:
        // https://github.com/OmniSharp/csharp-language-server-protocol/issues/644
        public async Task<LocationOrLocationLinks?> Handle(DefinitionParams request, CancellationToken cancellationToken)
#pragma warning restore CS8613 // Nullability of reference types in return type doesn't match implicitly implemented member.
        {
            _logger.LogInformation("Starting go-to-def endpoint request.");

            if (request is null)
            {
                _logger.LogWarning("Request is null.");
                throw new ArgumentNullException(nameof(request));
            }

            var documentSnapshot = await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                var path = request.TextDocument.Uri.GetAbsoluteOrUNCPath();
                _documentResolver.TryResolveDocument(path, out var documentSnapshot);
                return documentSnapshot;
            }, cancellationToken).ConfigureAwait(false);

            if (documentSnapshot is null)
            {
                _logger.LogWarning("Document snapshot is null for document.");
                return null;
            }

            if (!FileKinds.IsComponent(documentSnapshot.FileKind))
            {
                _logger.LogInformation($"FileKind '{documentSnapshot.FileKind}' is not a component type.");
                return null;
            }

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument.IsUnsupported())
            {
                _logger.LogInformation("Generated document is unsupported.");
                return null;
            }

            var originTagHelperBinding = await GetOriginTagHelperBindingAsync(documentSnapshot, codeDocument, request.Position, _logger).ConfigureAwait(false);
            if (originTagHelperBinding is null)
            {
                _logger.LogInformation("Origin TagHelper binding is null.");
                return null;
            }

            var originTagDescriptor = originTagHelperBinding.Descriptors.FirstOrDefault(d => !d.IsAttributeDescriptor());
            if (originTagDescriptor is null)
            {
                _logger.LogInformation("Origin TagHelper descriptor is null.");
                return null;
            }

            var originComponentDocumentSnapshot = await _componentSearchEngine.TryLocateComponentAsync(originTagDescriptor).ConfigureAwait(false);
            if (originComponentDocumentSnapshot is null)
            {
                _logger.LogInformation("Origin TagHelper document snapshot is null.");
                return null;
            }

            _logger.LogInformation($"Definition found at file path: {originComponentDocumentSnapshot.FilePath}");

            var originComponentUri = new UriBuilder
            {
                Path = originComponentDocumentSnapshot.FilePath,
                Scheme = Uri.UriSchemeFile,
                Host = string.Empty,
            }.Uri;

            return new LocationOrLocationLinks(new[]
            {
                new LocationOrLocationLink(new Location
                {
                    Uri = originComponentUri,
                    Range = new Range(new Position(0, 0), new Position(0, 0)),
                }),
            });
        }

        internal static async Task<TagHelperBinding?> GetOriginTagHelperBindingAsync(
            DocumentSnapshot documentSnapshot,
            RazorCodeDocument codeDocument,
            Position position,
            ILogger logger)
        {
            var sourceText = await documentSnapshot.GetTextAsync().ConfigureAwait(false);
            var linePosition = new LinePosition(position.Line, position.Character);
            var hostDocumentIndex = sourceText.Lines.GetPosition(linePosition);
            var location = new SourceLocation(hostDocumentIndex, position.Line, position.Character);

            var change = new SourceChange(location.AbsoluteIndex, length: 0, newText: string.Empty);
            var syntaxTree = codeDocument.GetSyntaxTree();
            if (syntaxTree?.Root is null)
            {
                logger.LogInformation("Could not retrieve syntax tree.");
                return null;
            }

            var owner = syntaxTree.Root.LocateOwner(change);
            if (owner is null)
            {
                logger.LogInformation("Could not locate owner.");
                return null;
            }

            var node = owner.Ancestors().FirstOrDefault(n =>
                n.Kind == SyntaxKind.MarkupTagHelperStartTag ||
                n.Kind == SyntaxKind.MarkupTagHelperEndTag);
            if (node is null)
            {
                logger.LogInformation("Could not locate ancestor of type MarkupTagHelperStartTag or MarkupTagHelperEndTag.");
                return null;
            }

            var name = GetStartOrEndTagName(node);
            if (name is null)
            {
                logger.LogInformation("Could not retrieve name of start or end tag.");
                return null;
            }

            if (!name.Span.Contains(location.AbsoluteIndex))
            {
                logger.LogInformation($"Tag name's span does not contain location's absolute index ({location.AbsoluteIndex}).");
                return null;
            }

            if (node.Parent is not MarkupTagHelperElementSyntax tagHelperElement)
            {
                logger.LogInformation("Parent of start or end tag is not a MarkupTagHelperElement.");
                return null;
            }

            return tagHelperElement.TagHelperInfo.BindingResult;
        }

        private static SyntaxNode? GetStartOrEndTagName(SyntaxNode node)
        {
            return node switch
            {
                MarkupTagHelperStartTagSyntax tagHelperStartTag => tagHelperStartTag.Name,
                MarkupTagHelperEndTagSyntax tagHelperEndTag => tagHelperEndTag.Name,
                _ => null
            };
        }
    }
}
