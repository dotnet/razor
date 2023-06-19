// Copyright (c) .NET Foundation. All rights reserved.
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
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.SpellCheck;

[LanguageServerEndpoint(VSInternalMethods.TextDocumentSpellCheckableRangesName)]
internal sealed class DocumentSpellCheckEndpoint : IRazorRequestHandler<VSInternalDocumentSpellCheckableParams, VSInternalSpellCheckableRangeReport[]>, IRegistrationExtension
{
    private readonly IRazorDocumentMappingService _documentMappingService;
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;
    private readonly ClientNotifierServiceBase _languageServer;

    public DocumentSpellCheckEndpoint(
        IRazorDocumentMappingService documentMappingService,
        LanguageServerFeatureOptions languageServerFeatureOptions,
        ClientNotifierServiceBase languageServer)
    {
        _documentMappingService = documentMappingService ?? throw new ArgumentNullException(nameof(documentMappingService));
        _languageServerFeatureOptions = languageServerFeatureOptions ?? throw new ArgumentNullException(nameof(languageServerFeatureOptions));
        _languageServer = languageServer ?? throw new ArgumentNullException(nameof(languageServer));
    }

    public bool MutatesSolutionState => false;

    public RegistrationExtensionResult GetRegistration(VSInternalClientCapabilities clientCapabilities)
    {
        const string ServerCapability = "_vs_spellCheckingProvider";

        return new RegistrationExtensionResult(ServerCapability, true);
    }

    public TextDocumentIdentifier GetTextDocumentIdentifier(VSInternalDocumentSpellCheckableParams request)
    {
        if (request.TextDocument is null)
        {
            throw new ArgumentNullException(nameof(request.TextDocument));
        }

        return request.TextDocument;
    }

    public async Task<VSInternalSpellCheckableRangeReport[]> HandleRequestAsync(VSInternalDocumentSpellCheckableParams request, RazorRequestContext requestContext, CancellationToken cancellationToken)
    {
        var documentContext = requestContext.GetRequiredDocumentContext();

        var razorRanges = await GetRazorSpellCheckRangesAsync(documentContext, cancellationToken).ConfigureAwait(false);
        var csharpRanges = await GetCSharpSpellCheckRangesAsync(documentContext, cancellationToken).ConfigureAwait(false);

        return razorRanges.Concat(csharpRanges).ToArray();
    }

    private static async Task<VSInternalSpellCheckableRangeReport[]> GetRazorSpellCheckRangesAsync(VersionedDocumentContext documentContext, CancellationToken cancellationToken)
    {
        var tree = await documentContext.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);

        using var _ = ListPool<VSInternalSpellCheckableRange>.GetPooledObject(out var ranges);

        foreach (var node in tree.Root.DescendantNodes())
        {
            if (node is RazorCommentBlockSyntax commentBlockSyntax)
            {
                var range = commentBlockSyntax.Comment.Span.AsRange(sourceText);
                ranges.Add(new VSInternalSpellCheckableRange
                {
                    Kind = VSInternalSpellCheckableRangeKind.Comment,
                    Start = range.Start,
                    End = range.End
                });
            }
            else if (node is MarkupTextLiteralSyntax textLiteralSyntax)
            {
                // Attribute names are text literals, but we don't want to spell check them because either C# will,
                // whether they're component attributes based on property names, or they come from tag helper attribute
                // parameters as strings, or they're Html attributes which are not necessarily expected to be real words.
                if (node.Parent is MarkupTagHelperAttributeSyntax or MarkupAttributeBlockSyntax)
                {
                    continue;
                }

                // Text literals appear everywhere in Razor to hold newlines and indentation, so its worth saving the tokens
                if (textLiteralSyntax.ContainsOnlyWhitespace())
                {
                    continue;
                }

                var range = textLiteralSyntax.Span.AsRange(sourceText);
                ranges.Add(new VSInternalSpellCheckableRange
                {
                    Kind = VSInternalSpellCheckableRangeKind.String,
                    Start = range.Start,
                    End = range.End
                });
            }
        }

        return new[] {
            new VSInternalSpellCheckableRangeReport
            {
                Ranges = ranges.ToArray(),
                ResultId = Guid.NewGuid().ToString()
            }
        };
    }

    private async Task<VSInternalSpellCheckableRangeReport[]> GetCSharpSpellCheckRangesAsync(VersionedDocumentContext documentContext, CancellationToken cancellationToken)
    {
        var delegatedParams = new DelegatedSpellCheckParams(documentContext.Identifier);
        var delegatedResponse = await _languageServer.SendRequestAsync<DelegatedSpellCheckParams, VSInternalSpellCheckableRangeReport[]?>(
            RazorLanguageServerCustomMessageTargets.RazorSpellCheckEndpoint,
            delegatedParams,
            cancellationToken).ConfigureAwait(false);

        if (delegatedResponse is null)
        {
            return Array.Empty<VSInternalSpellCheckableRangeReport>();
        }

        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var csharpDocument = codeDocument.GetCSharpDocument();

        using var _ = ListPool<VSInternalSpellCheckableRange>.GetPooledObject(out var ranges);

        foreach (var report in delegatedResponse)
        {
            if (report.Ranges is null)
            {
                continue;
            }

            ranges.SetCapacityIfLarger(report.Ranges.Length);
            foreach (var range in report.Ranges)
            {
                if (_documentMappingService.TryMapToHostDocumentRange(csharpDocument, range, out var hostDocumentRange))
                {
                    range.Start = hostDocumentRange.Start;
                    range.End = hostDocumentRange.End;
                    ranges.Add(range);
                }
            }

            report.Ranges = ranges.ToArray();
            ranges.Clear();
        }

        return delegatedResponse;
    }
}
