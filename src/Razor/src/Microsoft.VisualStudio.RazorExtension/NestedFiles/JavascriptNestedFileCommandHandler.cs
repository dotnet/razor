// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.CodeAnalysis.Razor.NestedFiles;
using Microsoft.VisualStudio.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.RazorExtension.NestedFiles;

/// <summary>
/// Command handler for JavaScript nested files (.razor.js).
/// </summary>
internal sealed class JavascriptNestedFileCommandHandler(
    IServiceProvider serviceProvider,
    Lazy<LSPRequestInvokerWrapper> requestInvoker)
    : NestedFileCommandHandler(serviceProvider, ".js", NestedFileKind.JavaScript, requestInvoker)
{
}
