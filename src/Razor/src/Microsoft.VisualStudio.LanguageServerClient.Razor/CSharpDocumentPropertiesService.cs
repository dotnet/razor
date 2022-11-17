﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.ExternalAccess.Razor;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor;

// Our IRazorDocumentPropertiesService services are our way to tell Roslyn to show C# diagnostics for files that are associated with the `DiagnosticsLspClientName`.
// Otherwise Roslyn would treat these documents as closed and would not provide any of their diagnostics.
internal sealed class CSharpDocumentPropertiesService : IRazorDocumentPropertiesService
{
    private const string RoslynRazorLanguageServerClientName = "RazorCSharp";

    public static readonly CSharpDocumentPropertiesService Instance = new();

    private CSharpDocumentPropertiesService()
    {
    }

    public bool DesignTimeOnly => false;

    public string? DiagnosticsLspClientName => RoslynRazorLanguageServerClientName;
}
