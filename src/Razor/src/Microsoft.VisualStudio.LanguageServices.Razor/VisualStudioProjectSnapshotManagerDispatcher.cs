// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.LanguageServices.Razor;

[Export(typeof(IProjectSnapshotManagerDispatcher))]
[method: ImportingConstructor]
internal class VisualStudioProjectSnapshotManagerDispatcher(IErrorReporter errorReporter) : SingleThreadProjectSnapshotManagerDispatcher(ThreadName)
{
    private const string ThreadName = "Razor." + nameof(VisualStudioProjectSnapshotManagerDispatcher);

    private readonly IErrorReporter _errorReporter = errorReporter;

    protected override void LogException(Exception ex)
        => _errorReporter.ReportError(ex);
}
