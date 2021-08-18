// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Razor
{
    internal class TestSolutionCloseTracker : SolutionCloseTracker
    {
        public new bool IsClosing
        {
            get
            {
                return base.IsClosing;
            }
            set
            {
                base.IsClosing = value;
            }
        }
    }
}
