// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.Editor.Razor;

namespace Microsoft.VisualStudio.Mac.LanguageServices.Razor
{
    [Export(typeof(RazorSolutionStatusService))]
    internal class VisualStudioWindowsSolutionStatusService : RazorSolutionStatusService
    {
        public override bool TryGetIntelliSenseStatus([NotNullWhen(returnValue: true)] out RazorSolutionStatus? status)
        {
            // In VS4Mac we'll just always fail to get status (it doesn't mean it's not ready, just means we can't query anything).
            status = null;
            return false;
        }
    }
}
