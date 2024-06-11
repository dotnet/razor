// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Protocol;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.RpcContracts.Settings;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal sealed class CapabilitiesManager(ILspServices lspServices)
    : IInitializeManager<InitializeParams, InitializeResult>, IClientCapabilitiesService, IWorkspaceRootPathProvider
{
    private readonly ILspServices _lspServices = lspServices;
    private InitializeParams? _initializeParams;

    public bool HasInitialized => _initializeParams is not null;

    public bool CanGetClientCapabilities => HasInitialized;

    public VSInternalClientCapabilities ClientCapabilities => GetInitializeParams().Capabilities.ToVSInternalClientCapabilities();

    public InitializeParams GetInitializeParams()
        => _initializeParams ??
           throw new InvalidOperationException($"{nameof(GetInitializeParams)} was called before '{Methods.InitializeName}'");

    public InitializeResult GetInitializeResult()
    {
        var initializeParams = GetInitializeParams();
        var clientCapabilities = initializeParams.Capabilities;
        var vsClientCapabilities = clientCapabilities.ToVSInternalClientCapabilities();

        var serverCapabilities = new VSInternalServerCapabilities();

        var capabilitiesProviders = _lspServices.GetRequiredServices<ICapabilitiesProvider>();
        foreach (var provider in capabilitiesProviders)
        {
            provider.ApplyCapabilities(serverCapabilities, vsClientCapabilities);
        }

        return new InitializeResult
        {
            Capabilities = serverCapabilities,
        };
    }

    public void SetInitializeParams(InitializeParams request)
    {
        _initializeParams = request;
    }

    public string GetRootPath()
    {
        var initializeParams = GetInitializeParams();

        if (initializeParams.RootUri is null)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            // RootUri was added in LSP3, fallback to RootPath
            return initializeParams.RootPath.AssumeNotNull();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        return initializeParams.RootUri.GetAbsoluteOrUNCPath();
    }
}
