// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.CommonLanguageServerProtocol.Framework;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;

internal readonly struct RazorRequestContext
{
    private readonly DocumentContext? _documentContext;

    public readonly ILspLogger LspLogger;

    public readonly ILogger Logger;

    public readonly ILspServices LspServices;

    public RazorRequestContext(
        DocumentContext? documentContext,
        ILspLogger lspLoger,
        ILogger logger,
        ILspServices lspServices)
    {
        _documentContext = documentContext;
        LspLogger = lspLoger;
        LspServices = lspServices;
        Logger = logger;
    }

    public DocumentContext GetRequiredDocumentContext()
    {
        if (_documentContext is null)
        {
            throw new ArgumentNullException(nameof(DocumentContext));
        }

        return _documentContext;
    }

    public T GetRequiredService<T>() where T : class
    {
        return LspServices.GetRequiredService<T>();
    }
}
