// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.CodeAnalysis.Razor.Workspaces
{
    internal static class JoinableTaskContextExtensions
    {
        public static void AssertUIThread(this JoinableTaskContext joinableTaskContext)
        {
            if (!joinableTaskContext.IsOnMainThread)
            {
                throw new InvalidOperationException(Resources.JoinableTaskContextExtensions_AssertUIThread);
            }
        }
    }
}
