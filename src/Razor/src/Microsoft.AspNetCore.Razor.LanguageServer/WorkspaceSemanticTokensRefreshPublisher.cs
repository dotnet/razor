// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal abstract class WorkspaceSemanticTokensRefreshPublisher : IDisposable
    {
        public abstract void Dispose();

        public abstract void PublishWorkspaceSemanticTokensRefresh();
    }
}
