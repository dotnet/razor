// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.CodeAnalysis.Razor.IsolationFiles;
using Microsoft.VisualStudio.Razor.ProjectSystem;

namespace Microsoft.VisualStudio.RazorExtension.IsolationFiles;

/// <summary>
/// Command handler for C# code-behind isolation files (.razor.cs).
/// </summary>
internal sealed class CSharpIsolationFileCommandHandler(
    IServiceProvider serviceProvider,
    Lazy<LSPRequestInvokerWrapper> requestInvoker)
    : IsolationFileCommandHandler(serviceProvider, ".cs", IsolationFileKind.CSharp, requestInvoker)
{
    protected override string AddText => Resources.Add_Code_Behind_File;

    protected override string ViewText => Resources.View_Code_Behind_File;
}
