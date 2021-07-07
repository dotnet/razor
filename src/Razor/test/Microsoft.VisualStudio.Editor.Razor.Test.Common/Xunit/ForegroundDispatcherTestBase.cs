// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language.Legacy;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Threading;

namespace Xunit
{
    public abstract class SingleThreadedDispatcherTestBase : ParserTestBase
    {
        internal SingleThreadedDispatcher Dispatcher { get; } = new TestSingleThreadedDispatcher();

        internal static JoinableTaskFactory JoinableTaskFactory
        {
            get
            {
                var joinableTaskContext = new JoinableTaskContextNode(new JoinableTaskContext());
                return new JoinableTaskFactory(joinableTaskContext.Context);
            }
        }
    }
}
