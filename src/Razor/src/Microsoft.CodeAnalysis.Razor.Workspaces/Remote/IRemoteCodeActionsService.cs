// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Roslyn.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal interface IRemoteCodeActionsService : IRemoteJsonService
{
    ValueTask<CodeAction[]> GetCodeActionsAsync(JsonSerializableRazorPinnedSolutionInfoWrapper solutionInfo, JsonSerializableDocumentId razorDocumentId, CodeActionParams request, CancellationToken cancellationToken);
}
