// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage.MessageInterception;
using Microsoft.VisualStudio.Utilities;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Export(typeof(MessageInterceptor))]
    [InterceptMethod(WorkspaceNames.SemanticTokensRefresh)]
    [ContentType(RazorLSPConstants.CSharpContentTypeName)]
    internal class RazorCSharpSemanticTokensInterceptor : MessageInterceptor
    {
        private readonly LSPRequestInvoker _requestInvoker;

        [ImportingConstructor]
        public RazorCSharpSemanticTokensInterceptor(LSPRequestInvoker requestInvoker)
        {
            if (requestInvoker is null)
            {
                throw new ArgumentNullException(nameof(requestInvoker));
            }

            _requestInvoker = requestInvoker;
        }

        public async override Task<InterceptionResult> ApplyChangesAsync(
            JToken message, string containedLanguageName, CancellationToken cancellationToken)
        {
            var refreshParams = new SemanticTokensRefreshParams();
            await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensRefreshParams, Unit>(
AspNetCore.Razor.LanguageServer.Common.RazorLanguageServerCustomMessageTargets.RazorSemanticTokensRefreshEndpoint,
                RazorLSPConstants.RazorLanguageServerName,
                refreshParams,
                cancellationToken).ConfigureAwait(false);

            return InterceptionResult.NoChange;
        }
    }
}
