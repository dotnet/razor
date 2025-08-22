// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Test.Common.Mef;
using Microsoft.AspNetCore.Razor.Test.Common.Workspaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Razor.Remote;
using Microsoft.VisualStudio.Composition;
using Microsoft.VisualStudio.Razor.LanguageClient.Cohost;
using Microsoft.VisualStudioCode.RazorExtension.Services;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudioCode.RazorExtension.Test;

public abstract class CohostEndpointTestBase(ITestOutputHelper testOutputHelper) : CohostTestBase(testOutputHelper)
{
    private VSCodeRemoteServiceInvoker? _remoteServiceInvoker;

    private protected override IRemoteServiceInvoker RemoteServiceInvoker => _remoteServiceInvoker.AssumeNotNull();

    /// <summary>
    /// The export provider for Roslyn "devenv" services, if tests opt-in to using them
    /// </summary>
    private protected ExportProvider? RoslynDevenvExportProvider { get; private set; }

    protected override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        var workspaceProvider = new VSCodeWorkspaceProvider();

        _remoteServiceInvoker = new VSCodeRemoteServiceInvoker(workspaceProvider, LoggerFactory);
        AddDisposable(_remoteServiceInvoker);
    }

    private protected override RemoteClientLSPInitializationOptions GetRemoteClientLSPInitializationOptions()
    {
        return new()
        {
            ClientCapabilities = new ClientCapabilities()
            {
                TextDocument = new TextDocumentClientCapabilities
                {
                    Completion = new CompletionSetting
                    {
                        CompletionItem = new CompletionItemSetting(),
                        CompletionItemKind = new CompletionItemKindSetting()
                        {
                            ValueSet = (CompletionItemKind[])Enum.GetValues(typeof(CompletionItemKind)),
                        },
                        CompletionListSetting = new CompletionListSetting()
                        {
                            ItemDefaults = ["commitCharacters", "editRange", "insertTextFormat", "data"]
                        },
                        ContextSupport = false,
                        InsertTextMode = InsertTextMode.AsIs,
                    }
                }
            }.ToVSInternalClientCapabilities(),
            TokenModifiers = [],
            TokenTypes = []
        };
    }
}
