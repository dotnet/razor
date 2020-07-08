// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using ILanguageServer = OmniSharp.Extensions.LanguageServer.Server.ILanguageServer;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class DefaultWorkspaceDirectoryResolver : WorkspaceDirectoryResolver
    {
        private readonly ILanguageServer _languageServer;

        public DefaultWorkspaceDirectoryResolver(ILanguageServer languageServer)
        {
            if (languageServer is null)
            {
                throw new ArgumentNullException(nameof(languageServer));
            }

            _languageServer = languageServer;
        }

        public override string Resolve()
        {
            if (_languageServer.ClientSettings.RootUri == null)
            {
                // RootUri was added in LSP3, fallback to RootPath
                return _languageServer.ClientSettings.RootPath;
            }

            return _languageServer.ClientSettings.RootUri.LocalPath;
        }
    }
}
