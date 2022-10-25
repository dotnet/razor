﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using DefinitionResult = Microsoft.VisualStudio.LanguageServer.Protocol.SumType<
    Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalLocation,
    Microsoft.VisualStudio.LanguageServer.Protocol.VSInternalLocation[],
    Microsoft.VisualStudio.LanguageServer.Protocol.DocumentLink[]>;
using SyntaxKind = Microsoft.AspNetCore.Razor.Language.SyntaxKind;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Definition
{
    internal class RazorDefinitionEndpoint : AbstractRazorDelegatingEndpoint<TextDocumentPositionParamsBridge, DefinitionResult?>, IDefinitionEndpoint
    {
        private readonly RazorComponentSearchEngine _componentSearchEngine;
        private readonly RazorDocumentMappingService _documentMappingService;

        public RazorDefinitionEndpoint(
            RazorComponentSearchEngine componentSearchEngine,
            RazorDocumentMappingService documentMappingService,
            LanguageServerFeatureOptions languageServerFeatureOptions,
            ClientNotifierServiceBase languageServer,
            ILoggerFactory loggerFactory)
            : base(languageServerFeatureOptions, documentMappingService, languageServer, loggerFactory.CreateLogger<RazorDefinitionEndpoint>())
        {
            _componentSearchEngine = componentSearchEngine ?? throw new ArgumentNullException(nameof(componentSearchEngine));
            _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
        }

        protected override string CustomMessageTarget => RazorLanguageServerCustomMessageTargets.RazorDefinitionEndpointName;

        public RegistrationExtensionResult GetRegistration(VSInternalClientCapabilities clientCapabilities)
        {
            const string ServerCapability = "definitionProvider";
            var option = new SumType<bool, DefinitionOptions>(new DefinitionOptions());

            return new RegistrationExtensionResult(ServerCapability, option);
        }

        protected async override Task<DefinitionResult?> TryHandleAsync(TextDocumentPositionParamsBridge request, RazorRequestContext requestContext, Projection projection, CancellationToken cancellationToken)
        {
            requestContext.Logger.LogInformation("Starting go-to-def endpoint request.");
            var documentContext = requestContext.GetRequiredDocumentContext();

            if (!FileKinds.IsComponent(documentContext.FileKind))
            {
                requestContext.Logger.LogInformation("FileKind '{fileKind}' is not a component type.", documentContext.FileKind);
                return default;
            }

            var (originTagDescriptor, attributeDescriptor) = await GetOriginTagHelperBindingAsync(documentContext, projection.AbsoluteIndex, requestContext.Logger, cancellationToken).ConfigureAwait(false);
            if (originTagDescriptor is null)
            {
                requestContext.Logger.LogInformation("Origin TagHelper descriptor is null.");
                return default;
            }

            var originComponentDocumentSnapshot = await _componentSearchEngine.TryLocateComponentAsync(originTagDescriptor).ConfigureAwait(false);
            if (originComponentDocumentSnapshot is null)
            {
                requestContext.Logger.LogInformation("Origin TagHelper document snapshot is null.");
                return default;
            }

            requestContext.Logger.LogInformation("Definition found at file path: {filePath}", originComponentDocumentSnapshot.FilePath);

            var range = await GetNavigateRangeAsync(originComponentDocumentSnapshot, attributeDescriptor, requestContext.Logger, cancellationToken);

            var originComponentUri = new UriBuilder
            {
                Path = originComponentDocumentSnapshot.FilePath,
                Scheme = Uri.UriSchemeFile,
                Host = string.Empty,
            }.Uri;

            return new[]
            {
                new VSInternalLocation
                {
                    Uri = originComponentUri,
                    Range = range,
                },
            };
        }

        protected override Task<IDelegatedParams?> CreateDelegatedParamsAsync(TextDocumentPositionParamsBridge request, RazorRequestContext requestContext, Projection projection, CancellationToken cancellationToken)
        {
            var documentContext = requestContext.GetRequiredDocumentContext();
            return Task.FromResult<IDelegatedParams?>(new DelegatedPositionParams(
                    documentContext.Identifier,
                    projection.Position,
                    projection.LanguageKind));
        }

        protected async override Task<DefinitionResult?> HandleDelegatedResponseAsync(DefinitionResult? response, TextDocumentPositionParamsBridge originalRequest, RazorRequestContext requestContext, Projection projection, CancellationToken cancellationToken)
        {
            if (response is null)
            {
                return null;
            }

            if (response.Value.TryGetFirst(out var location))
            {
                (location.Uri, location.Range) = await _documentMappingService.MapFromProjectedDocumentRangeAsync(location.Uri, location.Range, cancellationToken).ConfigureAwait(false);
            }
            else if (response.Value.TryGetSecond(out var locations))
            {
                foreach (var loc in locations)
                {
                    (loc.Uri, loc.Range) = await _documentMappingService.MapFromProjectedDocumentRangeAsync(loc.Uri, loc.Range, cancellationToken).ConfigureAwait(false);
                }
            }
            else if (response.Value.TryGetThird(out var links))
            {
                foreach (var link in links)
                {
                    if (link.Target is not null)
                    {
                        (link.Target, link.Range) = await _documentMappingService.MapFromProjectedDocumentRangeAsync(link.Target, link.Range, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            return response;
        }

        private async Task<Range> GetNavigateRangeAsync(DocumentSnapshot documentSnapshot, BoundAttributeDescriptor? attributeDescriptor, ILogger logger, CancellationToken cancellationToken)
        {
            if (attributeDescriptor is not null)
            {
                logger.LogInformation("Attempting to get definition from an attribute directly.");

                var originCodeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
                var range = await TryGetPropertyRangeAsync(originCodeDocument, attributeDescriptor.GetPropertyName(), _documentMappingService, logger, cancellationToken).ConfigureAwait(false);

                if (range is not null)
                {
                    return range;
                }
            }

            // When navigating from a start or end tag, we just take the user to the top of the file.
            // If we were trying to navigate to a property, and we couldn't find it, we can at least take
            // them to the file for the component. If the property was defined in a partial class they can
            // at least then press F7 to go there.
            return new Range { Start = new Position(0, 0), End = new Position(0, 0) };
        }

        internal static async Task<Range?> TryGetPropertyRangeAsync(RazorCodeDocument codeDocument, string propertyName, RazorDocumentMappingService documentMappingService, ILogger logger, CancellationToken cancellationToken)
        {
            // Parse the C# file and find the property that matches the name.
            // We don't worry about parameter attributes here for two main reasons:
            //   1. We don't have symbolic information, so the best we could do would be checking for any
            //      attribute named Parameter, regardless of which namespace. It also means we would have
            //      to do more checks for all of the various ways that the attribute could be specified
            //      (eg fully qualified, aliased, etc.)
            //   2. Since C# doesn't allow multiple properties with the same name, and we're doing a case
            //      sensitive search, we know the property we find is the one the user is trying to encode in a
            //      tag helper attribute. If they don't have the [Parameter] attribute then the Razor compiler
            //      will error, but allowing them to Go To Def on that property regardless, actually helps
            //      them fix the error.
            var csharpText = codeDocument.GetCSharpSourceText();
            var syntaxTree = CSharpSyntaxTree.ParseText(csharpText, cancellationToken: cancellationToken);
            var root = await syntaxTree.GetRootAsync(cancellationToken).ConfigureAwait(false);

            // Since we know how the compiler generates the C# source we can be a little specific here, and avoid
            // long tree walks. If the compiler ever changes how they generate their code, the tests for this will break
            // so we'll know about it.
            if (root is CompilationUnitSyntax compilationUnit &&
                compilationUnit.Members[0] is NamespaceDeclarationSyntax namespaceDeclaration &&
                namespaceDeclaration.Members[0] is ClassDeclarationSyntax classDeclaration)
            {
                var property = classDeclaration
                    .Members
                    .OfType<PropertyDeclarationSyntax>()
                    .Where(p => p.Identifier.ValueText.Equals(propertyName, StringComparison.Ordinal))
                    .FirstOrDefault();

                if (property is null)
                {
                    // The property probably exists in a partial class
                    logger.LogInformation("Could not find property in the generated source. Comes from partial?");
                    return null;
                }

                var range = property.Identifier.Span.AsRange(csharpText);
                if (documentMappingService.TryMapFromProjectedDocumentRange(codeDocument, range, out var originalRange))
                {
                    return originalRange;
                }

                logger.LogInformation("Property found but couldn't map its location.");
            }

            logger.LogInformation("Generated C# was not in expected shape (CompilationUnit -> Namespace -> Class)");

            return null;
        }

        internal static async Task<(TagHelperDescriptor?, BoundAttributeDescriptor?)> GetOriginTagHelperBindingAsync(
            DocumentContext documentContext,
            int absoluteIndex,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            var owner = await documentContext.GetSyntaxNodeAsync(absoluteIndex, cancellationToken).ConfigureAwait(false);
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
                // Normal attribute, ie <Component attribute=value />
                name = attribute.Name;
                propertyName = attribute.TagHelperAttributeInfo.Name;
            }
            else if (owner.Parent is MarkupMinimizedTagHelperAttributeSyntax minimizedAttribute)
            {
                // Minimized attribute, ie <Component attribute />
                name = minimizedAttribute.Name;
                propertyName = minimizedAttribute.TagHelperAttributeInfo.Name;
            }

            if (!name.Span.IntersectsWith(absoluteIndex))
            {
                logger.LogInformation("Tag name or attributes's span does not intersect with location's absolute index ({absoluteIndex}).", absoluteIndex);
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
                ? originTagDescriptor.BoundAttributes.FirstOrDefault(a => a.Name?.Equals(propertyName, StringComparison.Ordinal) == true)
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
