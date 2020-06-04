// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    public static class ILanguageServerExtensions
    {
        public static Task InitializedAsync(this ILanguageServer languageServer, CancellationToken cancellationToken)
        {
            var ls = languageServer as OmniSharp.Extensions.LanguageServer.Server.LanguageServer;
            var task = ls.Initialize(cancellationToken);

            return task;
        }
    }
}
