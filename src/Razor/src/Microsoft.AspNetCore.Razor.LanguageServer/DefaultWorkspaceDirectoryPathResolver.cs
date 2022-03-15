// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultWorkspaceDirectoryPathResolver : WorkspaceDirectoryPathResolver
    {
        private readonly IClientLanguageServer _languageServer;

        public DefaultWorkspaceDirectoryPathResolver(IClientLanguageServer languageServer!!)
        {
            _languageServer = languageServer;
        }

        public override string Resolve()
        {
            if (_languageServer.ClientSettings.RootUri is null)
            {
                // RootUri was added in LSP3, fallback to RootPath
                return _languageServer.ClientSettings.RootPath;
            }

            return _languageServer.ClientSettings.RootUri.GetFileSystemPath();
        }
    }
}
