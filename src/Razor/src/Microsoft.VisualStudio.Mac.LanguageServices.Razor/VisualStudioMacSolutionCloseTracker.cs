// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    [Export(typeof(SolutionCloseTracker))]
    [System.Composition.Shared]
    internal class VisualStudioMacSolutionCloseTracker : SolutionCloseTracker
    {
        [ImportingConstructor]
        public VisualStudioMacSolutionCloseTracker()
        {
        }
    }
}
