// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.CodeAnalysis.Razor.IsolationFiles;
using Microsoft.VisualStudio.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.RazorExtension.IsolationFiles;

/// <summary>
/// Command handler for CSS isolation files (.razor.css).
/// </summary>
internal sealed class CssIsolationFileCommandHandler(
    IServiceProvider serviceProvider,
    Lazy<LSPRequestInvokerWrapper> requestInvoker)
    : IsolationFileCommandHandler(serviceProvider, ".css", IsolationFileKind.Css, requestInvoker)
{
    protected override string AddText => Resources.Add_CSS_Isolation_File;

    protected override string ViewText => Resources.View_CSS_Isolation_File;
}
