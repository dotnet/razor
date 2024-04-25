﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Protocol.SemanticTokens;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Semantic;

public class RazorSemanticTokensRefreshEndpointTest(ITestOutputHelper testOutput) : ToolingTestBase(testOutput)
{
    [Fact]
    public async Task Handle_QueuesRefresh()
    {
        // Arrange
        var clientCapabilities = new VSInternalClientCapabilities
        {
            Workspace = new WorkspaceClientCapabilities
            {
                SemanticTokens = new SemanticTokensWorkspaceSetting
                {
                    RefreshSupport = true
                },
            },
        };
        var clientCapabilitiesService = new TestClientCapabilitiesService(clientCapabilities);
        var serverClient = new TestClient();
        var optionsMonitor = GetOptionsMonitor();
        using var publisher = new WorkspaceSemanticTokensRefreshNotifier(clientCapabilitiesService, serverClient, optionsMonitor);
        var refreshEndpoint = new RazorSemanticTokensRefreshEndpoint(publisher);
        var refreshParams = new SemanticTokensRefreshParams();
        var requestContext = new RazorRequestContext();

        // Act
        await refreshEndpoint.HandleNotificationAsync(refreshParams, requestContext, DisposalToken);
        await publisher.GetTestAccessor().WaitForNotificationAsync();

        // Assert
        Assert.Equal(Methods.WorkspaceSemanticTokensRefreshName, serverClient.Requests.Single().Method);
    }

    private static RazorLSPOptionsMonitor GetOptionsMonitor()
    {
        var configurationSyncService = new Mock<IConfigurationSyncService>(MockBehavior.Strict);

        var options = RazorLSPOptions.Default;
        configurationSyncService
            .Setup(c => c.GetLatestOptionsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult<RazorLSPOptions?>(options));

        var optionsMonitor = TestRazorLSPOptionsMonitor.Create(
            configurationSyncService.Object);

        return optionsMonitor;
    }
}
