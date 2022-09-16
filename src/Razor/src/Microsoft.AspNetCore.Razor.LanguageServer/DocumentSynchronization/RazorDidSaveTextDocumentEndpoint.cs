// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.LanguageServer.DocumentSynchronization;

internal class RazorDidSaveTextDocumentEndpoint : IVSDidSaveTextDocumentEndpoint
{
    public bool MutatesSolutionState => false;

    public TextDocumentIdentifier GetTextDocumentIdentifier(DidSaveTextDocumentParamsBridge request)
    {
        return request.TextDocument;
    }

    public Task HandleNotificationAsync(DidSaveTextDocumentParamsBridge request, RazorRequestContext context, CancellationToken cancellationToken)
    {
        context.LspLogger.LogInformation($"Saved Document {request.TextDocument.Uri.GetAbsoluteOrUNCPath()}");

        return Task.CompletedTask;
    }
}
