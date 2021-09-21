// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic;
using Microsoft.AspNetCore.Razor.LanguageServer.Semantic.Models;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using StreamJsonRpc;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    internal abstract class RazorLanguageServerCustomMessageTarget
    {
        // Called by the Razor Language Server to retrieve the user's latest settings.
        // NOTE: This method is a polyfill for VS. We only intend to do it this way until VS formally
        // supports sending workspace configuration requests.
        [JsonRpcMethod("workspace/configuration", UseSingleObjectParameterDeserialization = true)]
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

        // Called by the Razor Language Server to invoke a textDocument/rangeFormatting request
        // on the virtual Html/CSharp buffer.
        [JsonRpcMethod(LanguageServerConstants.RazorRangeFormattingEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<RazorDocumentRangeFormattingResponse> RazorRangeFormattingAsync(RazorDocumentRangeFormattingParams token, CancellationToken cancellationToken);

        // Called by the Razor Language Server to provide code actions from the platform.
        [JsonRpcMethod(LanguageServerConstants.RazorProvideCodeActionsEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<VSInternalCodeAction[]> ProvideCodeActionsAsync(CodeActionParams codeActionParams, CancellationToken cancellationToken);

        // Called by the Razor Language Server to resolve code actions from the platform.
        [JsonRpcMethod(LanguageServerConstants.RazorResolveCodeActionsEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<VSInternalCodeAction> ResolveCodeActionsAsync(VSInternalCodeAction codeAction, CancellationToken cancellationToken);

        // Called by the Razor Language Server to provide semantic tokens from the platform.
        [JsonRpcMethod(LanguageServerConstants.RazorProvideSemanticTokensEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<ProvideSemanticTokensResponse> ProvideSemanticTokensAsync(ProvideSemanticTokensParams semanticTokensParams, CancellationToken cancellationToken);

        // Called by the Razor Language Server to provide semantic tokens edits from the platform.
        [JsonRpcMethod(LanguageServerConstants.RazorProvideSemanticTokensEditsEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task<ProvideSemanticTokensEditsResponse> ProvideSemanticTokensEditsAsync(ProvideSemanticTokensDeltaParams semanticTokensParams, CancellationToken cancellationToken);

        [JsonRpcMethod(LanguageServerConstants.RazorServerReadyEndpoint, UseSingleObjectParameterDeserialization = true)]
        public abstract Task RazorServerReadyAsync(CancellationToken cancellationToken);
    }
}
