// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Mac.LanguageServices.Razor
{
    [Export(typeof(ProjectSnapshotManagerDispatcher))]
    internal class VisualStudioProjectSnapshotManagerDispatcher : ProjectSnapshotManagerDispatcherBase
    {
        private const string ThreadName = "Razor." + nameof(VisualStudioProjectSnapshotManagerDispatcher);
        private readonly ErrorReporter _errorReporter;

        public VisualStudioProjectSnapshotManagerDispatcher() : base(ThreadName)
        {
            _errorReporter = new VisualStudioErrorReporter();
        }

        public override void LogException(Exception ex) => _errorReporter.ReportError(ex);
    }
}
