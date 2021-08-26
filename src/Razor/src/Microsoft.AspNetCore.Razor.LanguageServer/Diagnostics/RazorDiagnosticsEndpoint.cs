// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Common.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.ProjectSystem;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using DiagnosticSeverity = OmniSharp.Extensions.LanguageServer.Protocol.Models.DiagnosticSeverity;
using RazorDiagnosticFactory = Microsoft.AspNetCore.Razor.Language.RazorDiagnosticFactory;
using SourceText = Microsoft.CodeAnalysis.Text.SourceText;
using SyntaxNode = Microsoft.AspNetCore.Razor.Language.Syntax.SyntaxNode;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Diagnostics
{
    internal class RazorDiagnosticsEndpoint :
        IRazorDiagnosticsHandler
    {
        // Internal for testing
        internal static readonly IReadOnlyCollection<string> CSharpDiagnosticsToIgnore = new HashSet<string>()
        {
            "RemoveUnnecessaryImportsFixable",
            "IDE0005_gen", // Using directive is unnecessary
        };

        private readonly ProjectSnapshotManagerDispatcher _projectSnapshotManagerDispatcher;
        private readonly DocumentResolver _documentResolver;
        private readonly DocumentVersionCache _documentVersionCache;
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly ILogger _logger;

        public RazorDiagnosticsEndpoint(
            ProjectSnapshotManagerDispatcher projectSnapshotManagerDispatcher,
            DocumentResolver documentResolver,
            DocumentVersionCache documentVersionCache,
            RazorDocumentMappingService documentMappingService,
            ILoggerFactory loggerFactory)
        {
            if (projectSnapshotManagerDispatcher == null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManagerDispatcher));
            }

            if (documentResolver == null)
            {
                throw new ArgumentNullException(nameof(documentResolver));
            }

            if (documentVersionCache == null)
            {
                throw new ArgumentNullException(nameof(documentVersionCache));
            }

            if (documentMappingService == null)
            {
                throw new ArgumentNullException(nameof(documentMappingService));
            }

            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _projectSnapshotManagerDispatcher = projectSnapshotManagerDispatcher;
            _documentResolver = documentResolver;
            _documentVersionCache = documentVersionCache;
            _documentMappingService = documentMappingService;
            _logger = loggerFactory.CreateLogger<RazorDiagnosticsEndpoint>();
        }

        public async Task<RazorDiagnosticsResponse> Handle(RazorDiagnosticsParams request, CancellationToken cancellationToken)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            _logger.LogInformation($"Received {request.Kind:G} diagnostic request for {request.RazorDocumentUri} with {request.Diagnostics.Length} diagnostics.");

            cancellationToken.ThrowIfCancellationRequested();

            int? documentVersion = null;
            DocumentSnapshot documentSnapshot = null;
            await _projectSnapshotManagerDispatcher.RunOnDispatcherThreadAsync(() =>
            {
                _documentResolver.TryResolveDocument(request.RazorDocumentUri.GetAbsoluteOrUNCPath(), out documentSnapshot);

                Debug.Assert(documentSnapshot != null, "Failed to get the document snapshot, could not map to document ranges.");

                if (documentSnapshot is null ||
                    !_documentVersionCache.TryGetDocumentVersion(documentSnapshot, out documentVersion))
                {
                    documentVersion = null;
                }
            }, cancellationToken).ConfigureAwait(false);

            if (documentSnapshot is null)
            {
                _logger.LogInformation($"Failed to find document {request.RazorDocumentUri}.");

                return new RazorDiagnosticsResponse()
                {
                    Diagnostics = null,
                    HostDocumentVersion = null
                };
            }

            var codeDocument = await documentSnapshot.GetGeneratedOutputAsync().ConfigureAwait(false);
            if (codeDocument?.IsUnsupported() != false)
            {
                _logger.LogInformation("Unsupported code document.");
                return new RazorDiagnosticsResponse()
                {
                    Diagnostics = Array.Empty<OmniSharpVSDiagnostic>(),
                    HostDocumentVersion = documentVersion
                };
            }

            var sourceText = await documentSnapshot.GetTextAsync();
            var unmappedDiagnostics = request.Diagnostics;
            var filteredDiagnostics = request.Kind == RazorLanguageKind.CSharp ?
                FilterCSharpDiagnostics(unmappedDiagnostics, codeDocument, sourceText) :
                FilterHTMLDiagnostics(unmappedDiagnostics, codeDocument, sourceText);
            if (!filteredDiagnostics.Any())
            {
                _logger.LogInformation("No diagnostics remaining after filtering.");

                return new RazorDiagnosticsResponse()
                {
                    Diagnostics = Array.Empty<OmniSharpVSDiagnostic>(),
                    HostDocumentVersion = documentVersion
                };
            }

            _logger.LogInformation($"{filteredDiagnostics.Length}/{unmappedDiagnostics.Length} diagnostics remain after filtering.");

            var mappedDiagnostics = MapDiagnostics(
                request.Kind,
                filteredDiagnostics,
                codeDocument,
                sourceText);

            _logger.LogInformation($"Returning {mappedDiagnostics.Length} mapped diagnostics.");

            return new RazorDiagnosticsResponse()
            {
                Diagnostics = mappedDiagnostics,
                HostDocumentVersion = documentVersion,
            };
        }

        private static OmniSharpVSDiagnostic[] FilterHTMLDiagnostics(
            OmniSharpVSDiagnostic[] unmappedDiagnostics,
            RazorCodeDocument codeDocument,
            SourceText sourceText)
        {
            var syntaxTree = codeDocument.GetSyntaxTree();

            var processedAttributes = new Dictionary<TextSpan, bool>();

            var filteredDiagnostics = unmappedDiagnostics
                .Where(d =>
                    !InAttributeContainingCSharp(d, sourceText, syntaxTree, processedAttributes) &&
                    !ShouldFilterHtmlDiagnosticBasedOnErrorCode(d, sourceText, syntaxTree))
                .ToArray();

            return filteredDiagnostics;
        }

#nullable enable
        private static bool ShouldFilterHtmlDiagnosticBasedOnErrorCode(OmniSharpVSDiagnostic diagnostic, SourceText sourceText, RazorSyntaxTree syntaxTree)
        {
            if (!diagnostic.Code.HasValue)
            {
                return false;
            }

            return diagnostic.Code.Value.String switch
            {
                CSSErrorCodes.MissingOpeningBrace => IsCSharpInStyleBlock(diagnostic, sourceText, syntaxTree),
                CSSErrorCodes.MissingSelectorAfterCombinator => IsCSharpInStyleBlock(diagnostic, sourceText, syntaxTree),
                CSSErrorCodes.MissingSelectorBeforeCombinatorCode => IsCSharpInStyleBlock(diagnostic, sourceText, syntaxTree),
                HtmlErrorCodes.UnexpectedEndTagErrorCode => IsHtmlWithBangAndMatchingTags(diagnostic, sourceText, syntaxTree),
                HtmlErrorCodes.InvalidNestingErrorCode => IsAnyFilteredInvalidNestingError(diagnostic, sourceText, syntaxTree),
                HtmlErrorCodes.MissingEndTagErrorCode => FileKinds.IsComponent(syntaxTree.Options.FileKind), // Redundant with RZ9980 in Components
                HtmlErrorCodes.TooFewElementsErrorCode => IsAnyFilteredTooFewElementsError(diagnostic, sourceText, syntaxTree),
                _ => false,
            };

            static bool IsCSharpInStyleBlock(OmniSharpVSDiagnostic d, SourceText sourceText, RazorSyntaxTree syntaxTree)
            {
                // C# in a style block causes diagnostics because the HTML background document replaces C# with "~"
                var owner = syntaxTree.GetOwner(sourceText, d.Range.Start);

                var element = owner.FirstAncestorOrSelf<MarkupElementSyntax>(
                    n => n.StartTag.Name.Content.Equals("style", StringComparison.Ordinal));
                var cSharp = owner.FirstAncestorOrSelf<CSharpCodeBlockSyntax>();

                return element.Body.Any(c => c is CSharpCodeBlockSyntax) || cSharp is not null;
            }

            // Ideally this would be solved instead by not emitting the "!" at the HTML backing file,
            // but we don't currently have a system to accomplish that
            static bool IsAnyFilteredTooFewElementsError(OmniSharpVSDiagnostic d, SourceText sourceText, RazorSyntaxTree syntaxTree)
            {
                var owner = syntaxTree.GetOwner(sourceText, d.Range.Start);
                var element = owner.FirstAncestorOrSelf<MarkupElementSyntax>();

                if (!element.StartTag.Name.Content.Equals("html", StringComparison.Ordinal))
                {
                    return false;
                }

                var bodyElement = (MarkupElementSyntax)element.ChildNodes().SingleOrDefault(c => c is MarkupElementSyntax tag && tag.StartTag.Name.Content.Equals("body", StringComparison.Ordinal));
                return bodyElement is not null && bodyElement.StartTag.Bang is not null;
            }

            // Ideally this would be solved instead by not emitting the "!" at the HTML backing file,
            // but we don't currently have a system to accomplish that
            static bool IsHtmlWithBangAndMatchingTags(OmniSharpVSDiagnostic d, SourceText sourceText, RazorSyntaxTree syntaxTree)
            {
                var owner = syntaxTree.GetOwner(sourceText, d.Range.Start);

                var element = owner.FirstAncestorOrSelf<MarkupElementSyntax>();
                var startNode = element.StartTag;
                var endNode = element.EndTag;

                if (startNode is null || endNode is null)
                {
                    // We only care about tags with a start and an end because we want to exclude diagnostics from their children
                    return false;
                }

                var haveBang = startNode.Bang != null && endNode.Bang != null;
                var namesEquivilant = startNode.Name.Content.Equals(endNode.Name.Content, StringComparison.Ordinal);

                return haveBang && namesEquivilant;
            }

            static bool IsAnyFilteredInvalidNestingError(OmniSharpVSDiagnostic d, SourceText sourceText, RazorSyntaxTree syntaxTree)
            {
                return IsInvalidNestingWarningWithinComponent(d, sourceText, syntaxTree) ||
                    IsInvalidNestingFromBody(d, sourceText, syntaxTree);
            }

            static bool IsInvalidNestingWarningWithinComponent(OmniSharpVSDiagnostic d, SourceText sourceText, RazorSyntaxTree syntaxTree)
            {
                var owner = syntaxTree.GetOwner(sourceText, d.Range.Start);

                var taghelperNode = owner.FirstAncestorOrSelf<MarkupTagHelperElementSyntax>();

                return !(taghelperNode is null);
            }

            // Ideally this would be solved instead by not emitting the "!" at the HTML backing file,
            // but we don't currently have a system to accomplish that
            static bool IsInvalidNestingFromBody(OmniSharpVSDiagnostic d, SourceText sourceText, RazorSyntaxTree syntaxTree)
            {
                var owner = syntaxTree.GetOwner(sourceText, d.Range.Start);
                var body = owner.FirstAncestorOrSelf<MarkupElementSyntax>(n => n.StartTag.Name.Content.Equals("body", StringComparison.Ordinal));

                if (ReferenceEquals(body, owner))
                {
                    return false;
                }

                if (d.Message is null)
                {
                    return false;
                }
                return d.Message.EndsWith("cannot be nested inside element 'html'.") && body.StartTag.Bang != null;
            }
        }
#nullable disable

        private static bool InAttributeContainingCSharp(
                OmniSharpVSDiagnostic d,
                SourceText sourceText,
                RazorSyntaxTree syntaxTree,
                Dictionary<TextSpan, bool> processedAttributes)
        {
            // Examine the _end_ of the diagnostic to see if we're at the
            // start of an (im/ex)plicit expression. Looking at the start
            // of the diagnostic isn't sufficient.
            if (d.Range is null)
            {
                return false;
            }

            var owner = syntaxTree.GetOwner(sourceText, d.Range.End);

            var markupAttributeNode = owner.FirstAncestorOrSelf<RazorSyntaxNode>(n =>
                n is MarkupAttributeBlockSyntax ||
                n is MarkupTagHelperAttributeSyntax ||
                n is MarkupMiscAttributeContentSyntax);

            if (markupAttributeNode != null)
            {
                if (!processedAttributes.TryGetValue(markupAttributeNode.FullSpan, out var doesAttributeContainNonMarkup))
                {
                    doesAttributeContainNonMarkup = CheckIfAttributeContainsNonMarkupNodes(markupAttributeNode);
                    processedAttributes.Add(markupAttributeNode.FullSpan, doesAttributeContainNonMarkup);
                }

                return doesAttributeContainNonMarkup;
            }

            return false;

            static bool CheckIfAttributeContainsNonMarkupNodes(RazorSyntaxNode attributeNode)
            {
                // Only allow markup, generic & (non-razor comment) token nodes
                var containsNonMarkupNodes = attributeNode.DescendantNodes()
                    .Any(n => !(n is MarkupBlockSyntax ||
                        n is MarkupSyntaxNode ||
                        n is GenericBlockSyntax ||
                        (n is SyntaxNode sn && sn.IsToken && sn.Kind != SyntaxKind.RazorCommentTransition)));
                return containsNonMarkupNodes;
            }
        }

        private OmniSharpVSDiagnostic[] FilterCSharpDiagnostics(OmniSharpVSDiagnostic[] unmappedDiagnostics, RazorCodeDocument codeDocument, SourceText sourceText)
        {
            return unmappedDiagnostics.Where(d =>
                !ShouldFilterCSharpDiagnosticBasedOnErrorCode(d, codeDocument, sourceText)).ToArray();
        }

        private bool ShouldFilterCSharpDiagnosticBasedOnErrorCode(OmniSharpVSDiagnostic diagnostic, RazorCodeDocument codeDocument, SourceText sourceText)
        {
            if (!diagnostic.Code.HasValue)
            {
                return false;
            }

            return diagnostic.Code.Value.String switch
            {
                "CS1525" => ShouldIgnoreCS1525(diagnostic, codeDocument, sourceText),
                _ => CSharpDiagnosticsToIgnore.Contains(diagnostic.Code.Value.String) &&
                        diagnostic.Severity != DiagnosticSeverity.Error,
            };

            bool ShouldIgnoreCS1525(OmniSharpVSDiagnostic diagnostic, RazorCodeDocument codeDocument, SourceText sourceText)
            {
                if (CheckIfDocumentHasRazorDiagnostic(codeDocument, RazorDiagnosticFactory.TagHelper_EmptyBoundAttribute.Id) &&
                    TryGetOriginalDiagnosticRange(diagnostic, codeDocument, sourceText, out var originalRange) &&
                    originalRange.IsUndefined())
                {
                    // Empty attribute values will take the following form in the generated C# document:
                    // __o = Microsoft.AspNetCore.Components.EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.ProgressEventArgs>(this, );
                    // The trailing `)` with no value preceding it, will lead to a C# error which doesn't make sense within the razor file.
                    // The empty attribute value is not directly mappable to Razor, hence we check if the diagnostic has an undefined range.
                    // Note; Error RZ2008 informs the user that the empty attribute value is not allowed.
                    // https://github.com/dotnet/aspnetcore/issues/30480
                    return true;
                }

                return false;
            }
        }

        // Internal & virtual for testing
        internal virtual bool CheckIfDocumentHasRazorDiagnostic(RazorCodeDocument codeDocument, string razorDiagnosticCode)
        {
            return codeDocument.GetSyntaxTree().Diagnostics.Any(d => d.Id.Equals(razorDiagnosticCode, StringComparison.Ordinal));
        }

        private OmniSharpVSDiagnostic[] MapDiagnostics(
            RazorLanguageKind languageKind,
            IReadOnlyList<OmniSharpVSDiagnostic> diagnostics,
            RazorCodeDocument codeDocument,
            SourceText sourceText)
        {
            if (languageKind != RazorLanguageKind.CSharp)
            {
                // All other non-C# requests map directly to where they are in the document.
                return diagnostics.ToArray();
            }

            var mappedDiagnostics = new List<OmniSharpVSDiagnostic>();

            for (var i = 0; i < diagnostics.Count; i++)
            {
                var diagnostic = diagnostics[i];

                if (!TryGetOriginalDiagnosticRange(diagnostic, codeDocument, sourceText, out var originalRange))
                {
                    continue;
                }

                diagnostic = diagnostic with { Range = originalRange };
                mappedDiagnostics.Add(diagnostic);
            }

            return mappedDiagnostics.ToArray();
        }

        private static bool IsRudeEditDiagnostic(OmniSharpVSDiagnostic diagnostic)
        {
            return diagnostic.Code.HasValue &&
                diagnostic.Code.Value.IsString &&
                diagnostic.Code.Value.String.StartsWith("ENC");
        }

        private bool TryRemapRudeEditRange(Range diagnosticRange, RazorCodeDocument codeDocument, SourceText sourceText, out Range remappedRange)
        {
            // This is a rude edit diagnostic that has already been mapped to the Razor document. The mapping isn't absolutely correct though,
            // it's based on the runtime code generation of the Razor document therefore we need to re-map the already mapped diagnostic in a
            // semi-intelligent way.

            var syntaxTree = codeDocument.GetSyntaxTree();
            var owner = syntaxTree.GetOwner(sourceText, diagnosticRange);

            switch (owner?.Kind)
            {
                case SyntaxKind.CSharpStatementLiteral: // Simple C# in @code block, @{ ... } etc.
                case SyntaxKind.CSharpExpressionLiteral: // Referenced simple C# in an implicit expression @Foo((abc) => {....})
                    // Good as is, we were able to find a known leaf-node that fully contains the diagnostic range. Therefore we can
                    // return the diagnostic range as is.
                    remappedRange = diagnosticRange;
                    return true;

                default:
                    // Unsupported owner of rude diagnostic, lets map to the entirety of the diagnostic range to be sure the diagnostic can be presented

                    _logger.LogInformation($"Failed to remap rude edit for SyntaxTree owner '{owner?.Kind}'.");

                    var startLineIndex = diagnosticRange.Start.Line;
                    if (startLineIndex >= sourceText.Lines.Count)
                    {
                        // Documents aren't sync'd we can't remap the ranges correctly, drop the diagnostic.
                        remappedRange = null;
                        return false;
                    }
                    var startLine = sourceText.Lines[startLineIndex];

                    // Look for the first non-whitespace character so we're not squiggling random whitespace at the start of the diagnostic
                    var firstNonWhitespaceCharacterOffset = sourceText.GetFirstNonWhitespaceOffset(startLine.Span, out _);
                    var diagnosticStartCharacter = firstNonWhitespaceCharacterOffset ?? 0;
                    var startLinePosition = new Position(startLineIndex, diagnosticStartCharacter);


                    var endLineIndex = diagnosticRange.End.Line;
                    if (endLineIndex >= sourceText.Lines.Count)
                    {
                        // Documents aren't sync'd we can't remap the ranges correctly, drop the diagnostic.
                        remappedRange = null;
                        return false;
                    }
                    var endLine = sourceText.Lines[endLineIndex];

                    // Look for the last non-whitespace character so we're not squiggling random whitespace at the end of the diagnostic
                    var lastNonWhitespaceCharacterOffset = sourceText.GetLastNonWhitespaceOffset(endLine.Span, out _);
                    var diagnosticEndCharacter = lastNonWhitespaceCharacterOffset ?? 0;
                    var diagnosticEndWhitespaceOffset = diagnosticEndCharacter + 1;
                    var endLinePosition = new Position(endLineIndex, diagnosticEndWhitespaceOffset);

                    remappedRange = new Range(startLinePosition, endLinePosition);
                    return true;
            }
        }

        private bool TryGetOriginalDiagnosticRange(OmniSharpVSDiagnostic diagnostic, RazorCodeDocument codeDocument, SourceText sourceText, out Range originalRange)
        {
            if (IsRudeEditDiagnostic(diagnostic))
            {
                if (TryRemapRudeEditRange(diagnostic.Range, codeDocument, sourceText, out originalRange))
                {
                    return true;
                }

                return false;
            }

            if (!_documentMappingService.TryMapFromProjectedDocumentRange(
                codeDocument,
                diagnostic.Range,
                MappingBehavior.Inclusive,
                out originalRange))
            {
                // Couldn't remap the range correctly.
                // If this isn't an `Error` Severity Diagnostic we can discard it.
                if (diagnostic.Severity != DiagnosticSeverity.Error)
                {
                    return false;
                }

                // For `Error` Severity diagnostics we still show the diagnostics to
                // the user, however we set the range to an undefined range to ensure
                // clicking on the diagnostic doesn't cause errors.
                originalRange = RangeExtensions.UndefinedRange;
            }

            return true;
        }
    }
}
