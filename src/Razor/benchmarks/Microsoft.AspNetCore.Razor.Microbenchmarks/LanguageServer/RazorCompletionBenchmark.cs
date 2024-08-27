﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion;
using Microsoft.AspNetCore.Razor.LanguageServer.Completion.Delegation;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis.Razor.DocumentMapping;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.LanguageServer;

public class RazorCompletionBenchmark : RazorLanguageServerBenchmarkBase
{
    private string? _filePath;
    private Uri? DocumentUri { get; set; }
    private RazorCompletionEndpoint? CompletionEndpoint { get; set; }
    private IDocumentSnapshot? DocumentSnapshot { get; set; }
    private SourceText? DocumentText { get; set; }
    private Position? RazorPosition { get; set; }
    private RazorRequestContext RazorRequestContext { get; set; }

    [GlobalSetup]
    public async Task SetupAsync()
    {
        var razorCompletionListProvider = RazorLanguageServerHost.GetRequiredService<RazorCompletionListProvider>();
        var lspServices = RazorLanguageServerHost.GetRequiredService<ILspServices>();
        var responseRewriters = lspServices.GetRequiredServices<DelegatedCompletionResponseRewriter>();
        var documentMappingService = lspServices.GetRequiredService<IDocumentMappingService>();
        var clientConnection = lspServices.GetRequiredService<IClientConnection>();
        var completionListCache = lspServices.GetRequiredService<CompletionListCache>();
        var loggerFactory = lspServices.GetRequiredService<ILoggerFactory>();

        var delegatedCompletionListProvider = new TestDelegatedCompletionListProvider(responseRewriters, documentMappingService, clientConnection, completionListCache);
        var completionListProvider = new CompletionListProvider(razorCompletionListProvider, delegatedCompletionListProvider);
        var configurationService = new DefaultRazorConfigurationService(clientConnection, loggerFactory);
        var optionsMonitor = new RazorLSPOptionsMonitor(configurationService, RazorLSPOptions.Default);

        CompletionEndpoint = new RazorCompletionEndpoint(completionListProvider, telemetryReporter: null, optionsMonitor);

        var clientCapabilities = new VSInternalClientCapabilities
        {
            TextDocument = new TextDocumentClientCapabilities
            {
                Completion = new VSInternalCompletionSetting
                {
                },
            },
        };
        CompletionEndpoint.ApplyCapabilities(new(), clientCapabilities);
        var projectRoot = Path.Combine(RepoRoot, "src", "Razor", "test", "testapps", "ComponentApp");
        var projectFilePath = Path.Combine(projectRoot, "ComponentApp.csproj");
        _filePath = Path.Combine(projectRoot, "Components", "Pages", $"Generated.razor");

        var content = GetFileContents();

        var razorCodeActionIndex = content.IndexOf("|R|");
        content = content.Replace("|R|", "");

        File.WriteAllText(_filePath, content);

        var targetPath = "/Components/Pages/Generated.razor";

        DocumentUri = new Uri(_filePath);
        DocumentSnapshot = await GetDocumentSnapshotAsync(projectFilePath, _filePath, targetPath);
        DocumentText = await DocumentSnapshot.GetTextAsync();

        RazorPosition = DocumentText.GetPosition(razorCodeActionIndex);

        var documentContext = new DocumentContext(DocumentUri, DocumentSnapshot, projectContext: null);
        RazorRequestContext = new RazorRequestContext(documentContext, RazorLanguageServerHost.GetRequiredService<ILspServices>(), "lsp/method", uri: null);
    }

    private static string GetFileContents()
    {
        var sb = new StringBuilder();

        sb.Append("""
            @using System;
            @using Endpoints.Pages;
            """);

        for (var i = 0; i < 100; i++)
        {
            sb.Append($$"""
            @{
                var y{{i}} = 456;
            }
            <div>
                <p>Hello there Mr {{i}}</p>
            </div>
            """);
        }

        sb.Append("""
            <div>
                <Ind|R|
                <span>@DateTime.Now</span>
                @if (true)
                {
                    <span>@y</span>
                }
            </div>
            @code {
                private void Goo()
                {
                }
            }"
            """);

        return sb.ToString();
    }

    [Benchmark(Description = "Razor Completion")]
    public async Task RazorCompletionAsync()
    {
        var completionParams = new CompletionParams
        {
            Position = RazorPosition!,
            Context = new VSInternalCompletionContext { },
            TextDocument = new TextDocumentIdentifier
            {
                Uri = DocumentUri!,
            },
        };

        var _ = await CompletionEndpoint!.HandleRequestAsync(completionParams, RazorRequestContext, CancellationToken.None);
    }

    private class TestDelegatedCompletionListProvider : DelegatedCompletionListProvider
    {
        public TestDelegatedCompletionListProvider(IEnumerable<DelegatedCompletionResponseRewriter> responseRewriters, IDocumentMappingService documentMappingService, IClientConnection clientConnection, CompletionListCache completionListCache)
            : base(responseRewriters, documentMappingService, clientConnection, completionListCache)
        {
        }

        public override Task<VSInternalCompletionList?> GetCompletionListAsync(int absoluteIndex, VSInternalCompletionContext completionContext, DocumentContext documentContext, VSInternalClientCapabilities clientCapabilities, Guid correlationId, CancellationToken cancellationToken)
        {
            return Task.FromResult<VSInternalCompletionList?>(
                new VSInternalCompletionList
                {
                });
        }
    }
}
