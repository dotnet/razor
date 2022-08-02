// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.DocumentPresentation;
using Microsoft.AspNetCore.Razor.LanguageServer.Folding;
using Microsoft.AspNetCore.Razor.LanguageServer.Protocol;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.WrapWithTag;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal abstract class RazorLanguageServerCustomMessageTarget
    {
        // Called by the Razor Language Server to retrieve the user's latest settings.
        // NOTE: This method is a polyfill for VS. We only intend to do it this way until VS formally
        // supports sending workspace configuration requests.
        [JsonRpcMethod(Methods.WorkspaceConfigurationName, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<object[]> WorkspaceConfigurationAsync(OmniSharp.Extensions.LanguageServer.Protocol.Models.ConfigurationParams configParams, CancellationToken cancellationToken);

        // Called by the Razor Language Server to update the contents of the virtual CSharp buffer.
        [JsonRpcMethod(LanguageServerConstants.RazorUpdateCSharpBufferEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task UpdateCSharpBufferAsync(UpdateBufferRequest token, CancellationToken cancellationToken);

        // Called by the Razor Language Server to update the contents of the virtual Html buffer.
        [JsonRpcMethod(LanguageServerConstants.RazorUpdateHtmlBufferEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task UpdateHtmlBufferAsync(UpdateBufferRequest token, CancellationToken cancellationToken);

        // Called by the Razor Language Server to invoke a textDocument/formatting request
        // on the virtual Html/CSharp buffer.
        [JsonRpcMethod(LanguageServerConstants.RazorDocumentFormattingEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<RazorDocumentRangeFormattingResponse> RazorDocumentFormattingAsync(DocumentFormattingParams token, CancellationToken cancellationToken);

        // Called by the Razor Language Server to invoke a textDocument/onTypeFormatting  request
        // on the virtual Html buffer.
        [JsonRpcMethod(LanguageServerConstants.RazorDocumentOnTypeFormattingEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<RazorDocumentRangeFormattingResponse> HtmlOnTypeFormattingAsync(RazorDocumentOnTypeFormattingParams token, CancellationToken cancellationToken);

        // Called by the Razor Language Server to invoke a textDocument/rangeFormatting request
        // on the virtual Html/CSharp buffer.
        [JsonRpcMethod(LanguageServerConstants.RazorRangeFormattingEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<RazorDocumentRangeFormattingResponse> RazorRangeFormattingAsync(RazorDocumentRangeFormattingParams token, CancellationToken cancellationToken);

        // Called by the Razor Language Server to provide code actions from the platform.
        [JsonRpcMethod(LanguageServerConstants.RazorProvideCodeActionsEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<IReadOnlyList<VSInternalCodeAction>?> ProvideCodeActionsAsync(CodeActionParams codeActionParams, CancellationToken cancellationToken);

        // Called by the Razor Language Server to resolve code actions from the platform.
        [JsonRpcMethod(LanguageServerConstants.RazorResolveCodeActionsEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<VSInternalCodeAction?> ResolveCodeActionsAsync(RazorResolveCodeActionParams codeAction, CancellationToken cancellationToken);

        // Called by the Razor Language Server to provide ranged semantic tokens from the platform.
        [JsonRpcMethod(LanguageServerConstants.RazorProvideSemanticTokensRangeEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<ProvideSemanticTokensResponse?> ProvideSemanticTokensRangeAsync(ProvideSemanticTokensRangeParams semanticTokensParams, CancellationToken cancellationToken);

        [JsonRpcMethod(LanguageServerConstants.RazorServerReadyEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task RazorServerReadyAsync(CancellationToken cancellationToken);

        // Called by Visual Studio to wrap the current selection with a tag
        [JsonRpcMethod(LanguageServerConstants.RazorWrapWithTagEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<VSInternalWrapWithTagResponse> RazorWrapWithTagAsync(VSInternalWrapWithTagParams wrapWithParams, CancellationToken cancellationToken);

        // Called by the Razor Language Server to provide inline completions from the platform.
        [JsonRpcMethod(LanguageServerConstants.RazorInlineCompletionEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<VSInternalInlineCompletionList?> ProvideInlineCompletionAsync(RazorInlineCompletionRequest inlineCompletionParams, CancellationToken cancellationToken);

        // Called by the Razor Language Server to provide document colors from the platform.
        [JsonRpcMethod(LanguageServerConstants.RazorProvideHtmlDocumentColorEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<IReadOnlyList<ColorInformation>> ProvideHtmlDocumentColorAsync(DocumentColorParams documentColorParams, CancellationToken cancellationToken);

        [JsonRpcMethod(LanguageServerConstants.RazorFoldingRangeEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<RazorFoldingRangeResponse?> ProvideFoldingRangesAsync(RazorFoldingRangeRequestParam foldingRangeParams, CancellationToken cancellationToken);

        [JsonRpcMethod(LanguageServerConstants.RazorTextPresentationEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<WorkspaceEdit?> ProvideTextPresentationAsync(RazorTextPresentationParams presentationParams, CancellationToken cancellationToken);

        [JsonRpcMethod(LanguageServerConstants.RazorUriPresentationEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<WorkspaceEdit?> ProvideUriPresentationAsync(RazorUriPresentationParams presentationParams, CancellationToken cancellationToken);

        [JsonRpcMethod(LanguageServerConstants.RazorCompletionEndpointName, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<JToken?> ProvideCompletionsAsync(DelegatedCompletionParams completionParams, CancellationToken cancellationToken);

        [JsonRpcMethod(LanguageServerConstants.RazorCompletionResolveEndpointName, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<JToken?> ProvideResolvedCompletionItemAsync(DelegatedCompletionItemResolveParams completionResolveParams, CancellationToken cancellationToken);

        [JsonRpcMethod(LanguageServerConstants.RazorGetFormattingOptionsEndpointName, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<FormattingOptions?> GetFormattingOptionsAsync(TextDocumentIdentifier document, CancellationToken cancellationToken);
    }
}
