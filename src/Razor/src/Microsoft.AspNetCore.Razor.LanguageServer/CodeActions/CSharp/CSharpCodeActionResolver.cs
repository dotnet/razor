// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions
{
    internal abstract class CSharpCodeActionResolver : BaseCodeActionResolver
    {
        protected readonly ClientNotifierServiceBase _languageServer;

        public CSharpCodeActionResolver(ClientNotifierServiceBase languageServer)
        {
            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            _languageServer = languageServer;
        }

        public abstract Task<RazorCodeAction> ResolveAsync(
            CSharpCodeActionParams csharpParams,
            RazorCodeAction codeAction,
            CancellationToken cancellationToken);

        protected async Task<RazorCodeAction> ResolveCodeActionWithServerAsync(RazorCodeAction codeAction, CancellationToken cancellationToken)
        {
            var response = await _languageServer.SendRequestAsync(LanguageServerConstants.RazorResolveCodeActionsEndpoint, codeAction).ConfigureAwait(false);
            var resolvedCodeAction = await response.Returning<RazorCodeAction>(cancellationToken).ConfigureAwait(false);

            return resolvedCodeAction;
        }
    }
}
