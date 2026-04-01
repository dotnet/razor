// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.CodeAnalysis.Razor.NestedFiles;
using Microsoft.VisualStudio.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.RazorExtension.NestedFiles;

/// <summary>
/// Command handler for CSS nested files (.razor.css).
/// </summary>
internal sealed class CssNestedFileCommandHandler(
    IServiceProvider serviceProvider,
    Lazy<LSPRequestInvokerWrapper> requestInvoker)
    : NestedFileCommandHandler(serviceProvider, ".css", NestedFileKind.Css, requestInvoker)
{
    protected override string AddText => Resources.Add_CSS_Nested_File;

    protected override string ViewText => Resources.View_CSS_Nested_File;
}
