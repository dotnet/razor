// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

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
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

using SyntaxKind = Microsoft.AspNetCore.Razor.Language.SyntaxKind;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Definition
{
    internal class RazorDefinitionEndpoint : IDefinitionHandler
    {
        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly RazorComponentSearchEngine _componentSearchEngine;
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly ILogger<RazorDefinitionEndpoint> _logger;

        public RazorDefinitionEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            RazorComponentSearchEngine componentSearchEngine,
            RazorDocumentMappingService documentMappingService,
            ILoggerFactory loggerFactory)
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher ?? throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            _documentResolver = documentResolver ?? throw new ArgumentNullException(nameof(documentResolver));
            _componentSearchEngine = componentSearchEngine ?? throw new ArgumentNullException(nameof(componentSearchEngine));
            _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
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

            var (originTagDescriptor, attributeDescriptor) = await GetOriginTagHelperBindingAsync(documentSnapshot, codeDocument, request.Position, _logger).ConfigureAwait(false);
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

            var range = await GetNavigateRangeAsync(originComponentDocumentSnapshot, attributeDescriptor, cancellationToken);

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
                    Range = range,
                }),
            });
        }

        private async Task<Range> GetNavigateRangeAsync(DocumentSnapshot documentSnapshot, BoundAttributeDescriptor? attributeDescriptor, CancellationToken cancellationToken)
        {
            if (attributeDescriptor is not null)
            {
                var originCodeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
                var range = await TryGetPropertyRangeAsync(originCodeDocument, attributeDescriptor.GetPropertyName(), _documentMappingService, cancellationToken).ConfigureAwait(false);

                if (range is not null)
                {
                    return range;
                }
            }

            return new Range(new Position(0, 0), new Position(0, 0));
        }

        internal static async Task<Range?> TryGetPropertyRangeAsync(RazorCodeDocument codeDocument, string propertyName, RazorDocumentMappingService documentMappingService, CancellationToken cancellationToken)
        {
            // Parse the C# file and find the property that matches the name, and that has a [Parameter] attribute

            var csharpText = codeDocument.GetCSharpSourceText();
            var syntaxTree = CSharpSyntaxTree.ParseText(csharpText, cancellationToken: cancellationToken);
            var root = await syntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);

            var property = root.DescendantNodes(n => n is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax or ClassDeclarationSyntax)
                .OfType<PropertyDeclarationSyntax>()
                .Where(p => p.Identifier.ValueText.Equals(propertyName, StringComparison.Ordinal))
                .FirstOrDefault();

            // Did we find a property at all?
            if (property is null)
            {
                return null;
            }

            // We might have found a property, but is it a parameter?
            foreach (var attributeList in property.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    // Attributes could be simple like [Parameter] or qualified like [Blah.Parameter]
                    // but we don't care about that distinction, so lets just avoid dealing with the complexities
                    // of the roslyn tree.
                    var name = attribute.ToString();

                    // Sadly we don't have symbolic information here, so we have to just hope for the best
                    // Since we're only navigating its not a big deal, plus its not possible to have multiple
                    // properties with the same name defined, so its not like one could be a real parameter, and
                    // another could be a fake one
                    if (name.Equals("ParameterAttribute", StringComparison.Ordinal) ||
                        name.Equals("Parameter", StringComparison.Ordinal) ||
                        name.EndsWith(".ParameterAttribute", StringComparison.Ordinal) ||
                        name.EndsWith(".Parameter", StringComparison.Ordinal))
                    {
                        var range = property.Identifier.Span.AsRange(csharpText);
                        if (documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, range, out var originalRange))
                        {
                            return originalRange;
                        }

                        // if we found the property we wanted, but couldn't map, there is no point doing any more work
                        return null;
                    }
                }
            }

            return null;
        }

        internal static async Task<(TagHelperDescriptor?, BoundAttributeDescriptor?)> GetOriginTagHelperBindingAsync(
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
                return (null, null);
            }

            var owner = syntaxTree.Root.LocateOwner(change);
            if (owner is null)
            {
                logger.LogInformation("Could not locate owner.");
                return (null, null);
            }

            var node = owner.Ancestors().FirstOrDefault(n =>
                n.Kind == SyntaxKind.MarkupTagHelperStartTag ||
                n.Kind == SyntaxKind.MarkupTagHelperEndTag);
            if (node is null)
            {
                logger.LogInformation("Could not locate ancestor of type MarkupTagHelperStartTag or MarkupTagHelperEndTag.");
                return (null, null);
            }

            var name = GetStartOrEndTagName(node);
            if (name is null)
            {
                logger.LogInformation("Could not retrieve name of start or end tag.");
                return (null, null);
            }

            string? propertyName = null;

            // If we're on an attribute then just validate against the attribute name
            if (owner.Parent is MarkupTagHelperAttributeSyntax attribute)
            {
                name = attribute.Name;
                propertyName = attribute.TagHelperAttributeInfo.Name;
            }

            if (!name.Span.Contains(location.AbsoluteIndex))
            {
                logger.LogInformation($"Tag name or attributes's span does not contain location's absolute index ({location.AbsoluteIndex}).");
                return (null, null);
            }

            if (node.Parent is not MarkupTagHelperElementSyntax tagHelperElement)
            {
                logger.LogInformation("Parent of start or end tag is not a MarkupTagHelperElement.");
                return (null, null);
            }

            var originTagDescriptor = tagHelperElement.TagHelperInfo.BindingResult.Descriptors.FirstOrDefault(d => !d.IsAttributeDescriptor());
            if (originTagDescriptor is null)
            {
                logger.LogInformation("Origin TagHelper descriptor is null.");
                return (null, null);
            }

            var attributeDescriptor = (propertyName is not null)
                ? originTagDescriptor.BoundAttributes.FirstOrDefault(a => a.Name.Equals(propertyName, StringComparison.Ordinal))
                : null;

            return (originTagDescriptor, attributeDescriptor);
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
