// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.Editor.Razor
{
    internal abstract class RazorSolutionStatus
    {
        public abstract bool IsAvailable { get; }

        public abstract event PropertyChangedEventHandler PropertyChanged;
    }
}
