// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.VisualStudio.Editor.Razor
{
    internal abstract class RazorSolutionStatusService
    {
        public abstract bool TryGetIntelliSenseStatus([NotNullWhen(returnValue: true)] out RazorSolutionStatus? status);
    }
}
