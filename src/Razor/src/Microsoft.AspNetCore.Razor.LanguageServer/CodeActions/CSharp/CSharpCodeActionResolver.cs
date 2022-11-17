// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.CodeActions.Models;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

internal abstract class CSharpCodeActionResolver : BaseDelegatedCodeActionResolver
{
    public CSharpCodeActionResolver(ClientNotifierServiceBase languageServer)
        : base(languageServer)
    {
    }

    public abstract Task<CodeAction> ResolveAsync(
        CodeActionResolveParams csharpParams,
        CodeAction codeAction,
        CancellationToken cancellationToken);
}
