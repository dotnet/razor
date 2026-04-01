// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.CodeAnalysis.Razor.NestedFiles;
using Microsoft.VisualStudio.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.RazorExtension.NestedFiles;

/// <summary>
/// Command handler for C# code-behind nested files (.razor.cs).
/// </summary>
internal sealed class CSharpNestedFileCommandHandler(
    IServiceProvider serviceProvider,
    Lazy<LSPRequestInvokerWrapper> requestInvoker)
    : NestedFileCommandHandler(serviceProvider, ".cs", NestedFileKind.CSharp, requestInvoker)
{
}
