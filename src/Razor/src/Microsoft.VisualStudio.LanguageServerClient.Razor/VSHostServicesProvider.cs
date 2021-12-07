// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.ComponentModel.Composition;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.CodeAnalysis.Host;
using Microsoft.VisualStudio.LanguageServices;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor
{
    [Export(typeof(VSHostServicesProvider))]
    internal class VSHostServicesProvider : HostServicesProvider
    {
        private readonly CodeAnalysis.Workspace _workspace;

        [ImportingConstructor]
        public VSHostServicesProvider([Import(typeof(VisualStudioWorkspace))] CodeAnalysis.Workspace workspace)
        {
            if (workspace is null)
            {
                throw new ArgumentNullException(nameof(workspace));
            }

            _workspace = workspace;
        }

        public override HostServices GetServices() => _workspace.Services.HostServices;
    }
}
