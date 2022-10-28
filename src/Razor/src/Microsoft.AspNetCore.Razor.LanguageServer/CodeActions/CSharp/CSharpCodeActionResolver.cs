// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal abstract class CSharpCodeActionResolver : BaseCodeActionResolver
    {
        protected readonly ClientNotifierServiceBase LanguageServer;

        public CSharpCodeActionResolver(ClientNotifierServiceBase languageServer)
        {
            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            LanguageServer = languageServer;
        }

        public abstract Task<CodeAction> ResolveAsync(
            CSharpCodeActionParams csharpParams,
            CodeAction codeAction,
            CancellationToken cancellationToken);

        protected async Task<CodeAction?> ResolveCodeActionWithServerAsync(Uri uri, CodeAction codeAction, CancellationToken cancellationToken)
        {
            var resolveCodeActionParams = new RazorResolveCodeActionParams(uri, codeAction);

            var resolvedCodeAction = await LanguageServer.SendRequestAsync<RazorResolveCodeActionParams, CodeAction?>(
                RazorLanguageServerCustomMessageTargets.RazorResolveCodeActionsEndpoint,
                resolveCodeActionParams,
                cancellationToken).ConfigureAwait(false);

            return resolvedCodeAction;
        }
    }
}
