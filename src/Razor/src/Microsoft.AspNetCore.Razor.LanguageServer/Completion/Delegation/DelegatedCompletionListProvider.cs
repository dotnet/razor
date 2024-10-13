// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Completion;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Protocol.Completion;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation;

internal class DelegatedCompletionListProvider
{
    private readonly ImmutableArray<DelegatedCompletionResponseRewriter> _responseRewriters;
    private readonly IDocumentMappingService _documentMappingService;
    private readonly IClientConnection _clientConnection;
    private readonly CompletionListCache _completionListCache;

    public DelegatedCompletionListProvider(
        IEnumerable<DelegatedCompletionResponseRewriter> responseRewriters,
        IDocumentMappingService documentMappingService,
        IClientConnection clientConnection,
        CompletionListCache completionListCache)
    {
        _responseRewriters = responseRewriters.OrderByAsArray(static x => x.Order);
        _documentMappingService = documentMappingService;
        _clientConnection = clientConnection;
        _completionListCache = completionListCache;
    }

    // virtual for tests
    public virtual FrozenSet<string> TriggerCharacters => CompletionTriggerCharacters.AllDelegationTriggerCharacters;

    // virtual for tests
    public virtual async Task<VSInternalCompletionList?> GetCompletionListAsync(
        int absoluteIndex,
        VSInternalCompletionContext completionContext,
        DocumentContext documentContext,
        VSInternalClientCapabilities clientCapabilities,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var positionInfo = await _documentMappingService
            .GetPositionInfoAsync(documentContext, absoluteIndex, cancellationToken)
            .ConfigureAwait(false);

        if (positionInfo.LanguageKind == RazorLanguageKind.Razor)
        {
            // Nothing to delegate to.
            return null;
        }

        var provisionalCompletion = await DelegatedCompletionHelper.TryGetProvisionalCompletionInfoAsync(
            documentContext,
            completionContext,
            positionInfo,
            _documentMappingService,
            cancellationToken).ConfigureAwait(false);
        TextEdit? provisionalTextEdit = null;
        if (provisionalCompletion is { } provisionalCompletionValue)
        {
            provisionalTextEdit = provisionalCompletionValue.ProvisionalTextEdit;
            positionInfo = provisionalCompletionValue.DocumentPositionInfo;
        }

        completionContext = DelegatedCompletionHelper.RewriteContext(completionContext, positionInfo.LanguageKind);

        var shouldIncludeSnippets = await ShouldIncludeSnippetsAsync(documentContext, absoluteIndex, cancellationToken).ConfigureAwait(false);

        var delegatedParams = new DelegatedCompletionParams(
            documentContext.GetTextDocumentIdentifierAndVersion(),
            positionInfo.Position,
            positionInfo.LanguageKind,
            completionContext,
            provisionalTextEdit,
            shouldIncludeSnippets,
            correlationId);

        var delegatedResponse = await _clientConnection.SendRequestAsync<DelegatedCompletionParams, VSInternalCompletionList?>(
            LanguageServerConstants.RazorCompletionEndpointName,
            delegatedParams,
            cancellationToken).ConfigureAwait(false);

        if (delegatedResponse is null)
        {
            // If we don't get a response from the delegated server, we have to make sure to return an incomplete completion
            // list. When a user is typing quickly, the delegated request from the first keystroke will fail to synchronize,
            // so if we return a "complete" list then the query won't re-query us for completion once the typing stops/slows
            // so we'd only ever return Razor completion items.
            return new VSInternalCompletionList() { IsIncomplete = true, Items = [] };
        }

        var rewrittenResponse = delegatedResponse;

        foreach (var rewriter in _responseRewriters)
        {
            rewrittenResponse = await rewriter.RewriteAsync(
                rewrittenResponse,
                absoluteIndex,
                documentContext,
                delegatedParams,
                cancellationToken).ConfigureAwait(false);
        }

        var completionCapability = clientCapabilities?.TextDocument?.Completion as VSInternalCompletionSetting;
        var resolutionContext = new DelegatedCompletionResolutionContext(delegatedParams, rewrittenResponse.Data);
        var resultId = _completionListCache.Add(rewrittenResponse, resolutionContext);
        rewrittenResponse.SetResultId(resultId, completionCapability);

        return rewrittenResponse;
    }

    private async Task<bool> ShouldIncludeSnippetsAsync(DocumentContext documentContext, int absoluteIndex, CancellationToken cancellationToken)
    {
        var codeDocument = await documentContext.GetCodeDocumentAsync(cancellationToken).ConfigureAwait(false);
        var tree = codeDocument.GetSyntaxTree();

        var token = tree.Root.FindToken(absoluteIndex, includeWhitespace: false);
        var node = token.Parent;
        var startOrEndTag = node?.FirstAncestorOrSelf<SyntaxNode>(n => RazorSyntaxFacts.IsAnyStartTag(n) || RazorSyntaxFacts.IsAnyEndTag(n));

        if (startOrEndTag is null)
        {
            return token.Kind is not (SyntaxKind.OpenAngle or SyntaxKind.CloseAngle);
        }

        if (startOrEndTag.Span.Start == absoluteIndex)
        {
            // We're at the start of the tag, we should include snippets. This is the case for things like $$<div></div> or <div>$$</div>, since the
            // index is right associative to the token when using FindToken.
            return true;
        }

        return !startOrEndTag.Span.Contains(absoluteIndex);
    }
}
