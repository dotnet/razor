// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces.Protocol.SemanticTokens;
using Microsoft.VisualStudio.LanguageServer.ContainedLanguage;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.Razor.LanguageClient;

[Export(typeof(IRazorCSharpInterceptionMiddleLayer))]
internal class RazorCSharpInterceptionMiddleLayer : IRazorCSharpInterceptionMiddleLayer
{
    private readonly LSPRequestInvoker _requestInvoker;
    private readonly ILogger _logger;

    [ImportingConstructor]
    public RazorCSharpInterceptionMiddleLayer(LSPRequestInvoker requestInvoker, ILoggerFactory loggerFactory)
    {
        _requestInvoker = requestInvoker;
        _logger = loggerFactory.GetOrCreateLogger<RazorCSharpInterceptionMiddleLayer>();
    }

    public bool CanHandle(string methodName)
    {
        return methodName.Equals(Methods.WorkspaceSemanticTokensRefreshName) ||
            methodName.Contains("textDocument/semanticTokens/range") ||
            methodName.Equals(Methods.TextDocumentDidChangeName);
    }

    public async Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification)
    {
        // IMPORTANT: This API shape is old and the methodParam and sendNotification parameters are likely to be null
        // as Roslyn moves away from Newtonsoft.Json. Do not use these parameters without designing a new API contract.
        //Debug.Assert(CanHandle(methodName), "Got a call to intercept a message we were not expecting");

        if (methodName.Equals(Methods.TextDocumentDidChangeName))
        {
            _logger.LogDebug($"Being asked if we can handle notification {methodName} for (editor) version {methodParam["textDocument"]?["version"]} of {methodParam["textDocument"]?["uri"]}");
            await sendNotification(methodParam).ConfigureAwait(false);
            _logger.LogDebug($"Finished notification {methodName} for (editor) version {methodParam["textDocument"]?["version"]} of {methodParam["textDocument"]?["uri"]}");
            return;
        }

        // Normally we use the LSPRequestInvoker when we're on the client side of things, and want
        // to send to a server, so at first glance this might seem redundant. However, we're actually
        // intercepting a message from the Roslyn server, so need to call into our server, so we can turn
        // around and call the client from our server, because its our server we want it to refresh
        // semantic tokens from.

        var refreshParams = new SemanticTokensRefreshParams();
        await _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensRefreshParams, Unit>(
            CustomMessageNames.RazorSemanticTokensRefreshEndpoint,
            RazorLSPConstants.RazorLanguageServerName,
            refreshParams,
            CancellationToken.None);
    }

    public async Task<JToken?> HandleRequestAsync(string methodName, JToken methodParam, Func<JToken, Task<JToken?>> sendRequest)
    {
        if (methodName.Contains("textDocument/semanticTokens/range"))
        {
            _logger.LogDebug($"Being asked if we can handle request {methodName} for version {methodParam["textDocument"]?["version"]} of {methodParam["textDocument"]?["uri"]}");
            var result = await sendRequest(methodParam).ConfigureAwait(false);
            _logger.LogDebug($"Finished request {methodName} for version {methodParam["textDocument"]?["version"]} of {methodParam["textDocument"]?["uri"]}");
            return result;
        }

        return await sendRequest(methodParam);
    }

    // A basic POCO which will handle the lack of data in the response.
    private class Unit
    {

    }
}
