// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Razor.LanguageServer.CodeActions;

internal interface IRazorCodeActionResolver : ICodeActionResolver
{
    Task<WorkspaceEdit?> ResolveAsync(JsonElement data, CancellationToken cancellationToken);
}
