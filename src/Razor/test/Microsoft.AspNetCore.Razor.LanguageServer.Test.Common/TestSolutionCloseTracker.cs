// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Razor;

namespace Microsoft.AspNetCore.Razor.Test.Common
{
    internal class TestSolutionCloseTracker : SolutionCloseTracker
    {
        public new bool IsClosing { get; set; }
    }
}
