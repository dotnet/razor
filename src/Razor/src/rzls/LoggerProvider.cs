// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Hosting;
using Microsoft.CodeAnalysis.Razor.Logging;

namespace Microsoft.AspNetCore.Razor.LanguageServer;

internal class LoggerProvider(LogLevelProvider logLevelProvider, IClientConnection clientConnection) : ILoggerProvider
{
    private readonly IClientConnection _clientConnection = clientConnection;

    public ILogger CreateLogger(string categoryName)
    {
        return new LspLogger(categoryName, logLevelProvider, _clientConnection);
    }
}
