// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Razor.Protocol;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal abstract class AbstractFilePathService(LanguageServerFeatureOptions languageServerFeatureOptions) : IFilePathService
{
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions = languageServerFeatureOptions;

    public virtual Uri GetRazorDocumentUri(Uri virtualDocumentUri)
    {
        var uriPath = virtualDocumentUri.AbsoluteUri;
        var razorFilePath = GetRazorFilePath(uriPath);
        var uri = new Uri(razorFilePath, UriKind.Absolute);
        return uri;
    }

    public virtual bool IsVirtualCSharpFile(Uri uri)
        => CheckIfFileUriAndExtensionMatch(uri, LanguageServerConstants.CSharpVirtualDocumentSuffix);

    public bool IsVirtualHtmlFile(Uri uri)
        => CheckIfFileUriAndExtensionMatch(uri, LanguageServerConstants.HtmlVirtualDocumentSuffix);

    public bool IsVirtualDocumentUri(Uri uri)
        => IsVirtualCSharpFile(uri) || IsVirtualHtmlFile(uri);

    private static bool CheckIfFileUriAndExtensionMatch(Uri uri, string extension)
        => uri.GetAbsoluteOrUNCPath()?.EndsWith(extension, StringComparison.Ordinal) ?? false;

    private string GetRazorFilePath(string filePath)
    {
        var trimIndex = filePath.LastIndexOf(LanguageServerConstants.HtmlVirtualDocumentSuffix);

        // We don't check for C# in cohosting, as it will throw, and people might call this method on any
        // random path.
        if (trimIndex == -1 && !_languageServerFeatureOptions.UseRazorCohostServer)
        {
            trimIndex = filePath.LastIndexOf(LanguageServerConstants.CSharpVirtualDocumentSuffix);

            if (trimIndex == -1)
            {
                // Not a C# file, and we wouldn't have got here if it was Html, so nothing left to do
                return filePath;
            }

            // If this is a C# generated file, then filename will be <Page>.razor.<project slug><c# suffix>
            // We can remove the project key easily, by just looking for the last '.'. The project
            // slug itself cannot a '.', enforced by the assert below in GetProjectSuffix
            trimIndex = filePath.LastIndexOf('.', trimIndex - 1);
            Debug.Assert(trimIndex != -1, "There was no project element to the generated file name?");
        }

        if (trimIndex != -1)
        {
            return filePath[..trimIndex];
        }

        return filePath;
    }

    internal TestAccessor GetTestAccessor() => new(this);

    internal readonly struct TestAccessor(AbstractFilePathService instance)
    {
        internal string GetRazorFilePath(string filePath) => instance.GetRazorFilePath(filePath);
    }
}
