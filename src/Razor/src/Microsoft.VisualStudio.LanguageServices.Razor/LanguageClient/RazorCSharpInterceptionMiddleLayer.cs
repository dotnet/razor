// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.ExternalAccess.Razor;
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

    [ImportingConstructor]
    public RazorCSharpInterceptionMiddleLayer(LSPRequestInvoker requestInvoker)
    {
        _requestInvoker = requestInvoker;
    }

    public bool CanHandle(string methodName)
        => methodName.Equals(Methods.WorkspaceSemanticTokensRefreshName);

    public Task HandleNotificationAsync(string methodName, JToken methodParam, Func<JToken, Task> sendNotification)
    {
        // IMPORTANT: This API shape is old and the methodParam and sendNotification parameters are likely to be null
        // as Roslyn moves away from Newtonsoft.Json. Do not use these parameters without designing a new API contract.
        Debug.Assert(CanHandle(methodName), "Got a call to intercept a message we were not expecting");

        var refreshParams = new SemanticTokensRefreshParams();
        return _requestInvoker.ReinvokeRequestOnServerAsync<SemanticTokensRefreshParams, Unit>(
            CustomMessageNames.RazorSemanticTokensRefreshEndpoint,
            RazorLSPConstants.RazorLanguageServerName,
            refreshParams,
            CancellationToken.None);
    }

    public Task<JToken?> HandleRequestAsync(string methodName, JToken methodParam, Func<JToken, Task<JToken?>> sendRequest)
        => throw new NotImplementedException();

    // A basic POCO which will handle the lack of data in the response.
    private class Unit
    {

    }
}
