// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation
{
    internal class DelegatedCompletionListProvider : CompletionListProvider
    {
        private static readonly IReadOnlyList<string> s_razorTriggerCharacters = new[] { "@" };
        private static readonly IReadOnlyList<string> s_cSharpTriggerCharacters = new[] { " ", "(", "=", "#", ".", "<", "[", "{", "\"", "/", ":", "~" };
        private static readonly IReadOnlyList<string> s_htmlTriggerCharacters = new[] { ":", "@", "#", ".", "!", "*", ",", "(", "[", "-", "<", "&", "\\", "/", "'", "\"", "=", ":", " ", "`" };
        private static readonly ImmutableHashSet<string> s_allTriggerCharacters =
            s_cSharpTriggerCharacters
                .Concat(s_htmlTriggerCharacters)
                .Concat(s_razorTriggerCharacters)
                .ToImmutableHashSet();

        private readonly IReadOnlyList<DelegatedCompletionResponseRewriter> _responseRewriters;
        private readonly RazorDocumentMappingService _documentMappingService;
        private readonly ClientNotifierServiceBase _languageServer;

        public DelegatedCompletionListProvider(
            IEnumerable<DelegatedCompletionResponseRewriter> responseRewriters,
            RazorDocumentMappingService documentMappingService,
            ClientNotifierServiceBase languageServer)
        {
            _responseRewriters = responseRewriters.OrderBy(rewriter => rewriter.Order).ToArray();
            _documentMappingService = documentMappingService;
            _languageServer = languageServer;
        }

        public override ImmutableHashSet<string> TriggerCharacters => s_allTriggerCharacters;

        public override async Task<VSInternalCompletionList?> GetCompletionListAsync(
            int absoluteIndex,
            VSInternalCompletionContext completionContext,
            DocumentContext documentContext,
            VSInternalClientCapabilities clientCapabilities,
            CancellationToken cancellationToken)
        {
            var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
            var sourceText = await documentContext.GetSourceTextAsync(cancellationToken).ConfigureAwait(false);
            var projection = GetProjection(absoluteIndex, codeDocument, sourceText);

            completionContext = RewriteContext(completionContext, projection.LanguageKind);

            var delegatedParams = new DelegatedCompletionParams(
                documentContext.Identifier,
                projection.Position,
                projection.LanguageKind,
                completionContext);
            var delegatedRequest = await _languageServer.SendRequestAsync(LanguageServerConstants.RazorCompletionEndpointName, delegatedParams).ConfigureAwait(false);
            var delegatedResponse = await delegatedRequest.Returning<VSInternalCompletionList?>(cancellationToken).ConfigureAwait(false);

            if (delegatedResponse is null)
            {
                return null;
            }

            var rewrittenCompletionList = delegatedResponse;
            foreach (var rewriter in _responseRewriters)
            {
                rewrittenCompletionList = await rewriter.RewriteAsync(rewrittenCompletionList,
                                                                      absoluteIndex,
                                                                      documentContext,
                                                                      delegatedParams,
                                                                      cancellationToken).ConfigureAwait(false);
            }

            return rewrittenCompletionList;
        }

        private CompletionProjection GetProjection(int absoluteIndex, RazorCodeDocument codeDocument, SourceText sourceText)
        {
            sourceText.GetLineAndOffset(absoluteIndex, out var line, out var character);
            var projectedPosition = new Position(line, character);

            var languageKind = _documentMappingService.GetLanguageKind(codeDocument, absoluteIndex, rightAssociative: false);
            if (languageKind == RazorLanguageKind.CSharp)
            {
                if (_documentMappingService.TryMapToProjectedDocumentPosition(codeDocument, absoluteIndex, out var mappedPosition, out _))
                {
                    // For C# locations, we attempt to return the corresponding position
                    // within the projected document
                    projectedPosition = mappedPosition;
                }
                else
                {
                    // It no longer makes sense to think of this location as C#, since it doesn't
                    // correspond to any position in the projected document. This should not happen
                    // since there should be source mappings for all the C# spans.
                    languageKind = RazorLanguageKind.Razor;
                }
            }

            return new CompletionProjection(languageKind, projectedPosition);
        }

        private static VSInternalCompletionContext RewriteContext(VSInternalCompletionContext context, RazorLanguageKind languageKind)
        {
            if (context.TriggerKind != CompletionTriggerKind.TriggerCharacter)
            {
                // Non-triggered based completion, the existing context is valid.
                return context;
            }

            if (languageKind == RazorLanguageKind.CSharp && s_cSharpTriggerCharacters.Contains(context.TriggerCharacter))
            {
                // C# trigger character for C# content
                return context;
            }

            if (languageKind == RazorLanguageKind.Html && s_htmlTriggerCharacters.Contains(context.TriggerCharacter))
            {
                // HTML trigger character for HTML content
                return context;
            }

            // Trigger character not associated with the current langauge. Transform the context into an invoked context.
            var rewrittenContext = new VSInternalCompletionContext()
            {
                InvokeKind = context.InvokeKind,
                TriggerKind = CompletionTriggerKind.Invoked,
            };

            if (languageKind == RazorLanguageKind.CSharp && s_razorTriggerCharacters.Contains(context.TriggerCharacter))
            {
                // The C# language server will not return any completions for the '@' character unless we
                // send the completion request explicitly.
                rewrittenContext.InvokeKind = VSInternalCompletionInvokeKind.Explicit;
            }

            return rewrittenContext;
        }

        private record class CompletionProjection(RazorLanguageKind LanguageKind, Position Position);
    }
}
