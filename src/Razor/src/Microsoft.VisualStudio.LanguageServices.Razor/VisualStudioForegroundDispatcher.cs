// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.LanguageServices.Razor
{
    [Export(typeof(ForegroundDispatcher))]
    internal class VisualStudioForegroundDispatcher : DefaultForegroundDispatcher
    {
        private readonly JoinableTaskContext _joinableTaskContext;

        [ImportingConstructor]
        public VisualStudioForegroundDispatcher(JoinableTaskContext joinableTaskContext)
        {
            _joinableTaskContext = joinableTaskContext;
        }

        public override bool IsBackgroundThread => !IsForegroundThread && !_joinableTaskContext.IsOnMainThread;
    }
}
