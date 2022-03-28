// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Editor.Razor;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    [Export(typeof(RazorUIContextManager))]
    internal class VisualStudioMacRazorUIContextManager : RazorUIContextManager
    {
        public override Task SetUIContextAsync(Guid uiContextGuid, bool isActive, CancellationToken cancellationToken)
        {
            // UIContext's aren't a thing in VS4Mac.
            return Task.CompletedTask;
        }
    }
}
