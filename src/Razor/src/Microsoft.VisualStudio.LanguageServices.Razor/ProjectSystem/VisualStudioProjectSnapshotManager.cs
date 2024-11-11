﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System.ComponentModel.Composition;
using Microsoft.AspNetCore.Razor.ProjectEngineHost;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.VisualStudio.Razor.ProjectSystem;

[Export(typeof(IProjectSnapshotManager))]
[method: ImportingConstructor]
internal sealed class VisualStudioProjectSnapshotManager(
    IProjectEngineFactoryProvider projectEngineFactoryProvider,
    LanguageServerFeatureOptions languageServerFeatureOptions,
    ILoggerFactory loggerFactory)
    : ProjectSnapshotManager(projectEngineFactoryProvider, languageServerFeatureOptions, loggerFactory)
{
}
