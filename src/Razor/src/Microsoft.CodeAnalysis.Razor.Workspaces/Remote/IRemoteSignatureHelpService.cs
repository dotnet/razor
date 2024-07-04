// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces.Protocol.SignatureHelp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Remote;

internal interface IRemoteSignatureHelpService
{
    ValueTask<RemoteSignatureHelp?> GetSignatureHelpAsync(RazorPinnedSolutionInfoWrapper solutionInfo, DocumentId id, LinePosition linePosition, SignatureHelpTriggerKind triggerKind, string? triggerCharacter, CancellationToken cancellationToken);
}
