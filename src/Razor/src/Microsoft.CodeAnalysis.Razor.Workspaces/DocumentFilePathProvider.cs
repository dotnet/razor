// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;
using System.Diagnostics;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

[Shared]
[Export(typeof(DocumentFilePathProvider))]
internal class DocumentFilePathProvider
{
    private readonly LanguageServerFeatureOptions _languageServerFeatureOptions;

    [ImportingConstructor]
    public DocumentFilePathProvider(LanguageServerFeatureOptions languageServerFeatureOptions)
    {
        _languageServerFeatureOptions = languageServerFeatureOptions;
    }

    public string GetRazorCSharpFilePath(ProjectKey projectKey, string razorFilePath)
        => GetGeneratedFilePath(projectKey, razorFilePath, _languageServerFeatureOptions.CSharpVirtualDocumentSuffix);

    public string GetRazorHtmlFilePath(ProjectKey projectKey, string razorFilePath)
        => GetGeneratedFilePath(projectKey, razorFilePath, _languageServerFeatureOptions.HtmlVirtualDocumentSuffix);

    public Uri GetRazorDocumentUri(Uri virtualDocumentUri)
    {
        var uriPath = virtualDocumentUri.AbsoluteUri;
        var razorFilePath = GetRazorFilePath(uriPath);
        var uri = new Uri(razorFilePath, UriKind.Absolute);
        return uri;
    }

    public bool IsVirtualCSharpFile(Uri uri)
        => CheckIfFileUriAndExtensionMatch(uri, _languageServerFeatureOptions.CSharpVirtualDocumentSuffix);

    public bool IsVirtualHtmlFile(Uri uri)
        => CheckIfFileUriAndExtensionMatch(uri, _languageServerFeatureOptions.HtmlVirtualDocumentSuffix);

    public bool IsVirtualDocumentUri(Uri uri)
        => IsVirtualCSharpFile(uri) || IsVirtualHtmlFile(uri);

    private static bool CheckIfFileUriAndExtensionMatch(Uri uri, string extension)
        => uri.GetAbsoluteOrUNCPath()?.EndsWith(extension, StringComparison.Ordinal) ?? false;

    private string GetRazorFilePath(string filePath)
    {
        var trimIndex = filePath.LastIndexOf(_languageServerFeatureOptions.CSharpVirtualDocumentSuffix);
        if (trimIndex == -1)
        {
            trimIndex = filePath.LastIndexOf(_languageServerFeatureOptions.HtmlVirtualDocumentSuffix);
        }

        if (trimIndex != -1 && _languageServerFeatureOptions.IncludeProjectKeyInGeneratedFilePath)
        {
            trimIndex = filePath.LastIndexOf('.', trimIndex);
            Debug.Assert(trimIndex != -1, "There was no project element to the generated file name?");
        }

        if (trimIndex != -1)
        {
            return filePath.Substring(0, trimIndex);
        }

        return filePath;
    }

    private string GetGeneratedFilePath(ProjectKey projectKey, string razorFilePath, string suffix)
    {
        var projectSuffix = GetProjectSuffix(projectKey);

        return razorFilePath + projectSuffix + suffix;
    }

    private string GetProjectSuffix(ProjectKey _)
    {
        if (!_languageServerFeatureOptions.IncludeProjectKeyInGeneratedFilePath)
        {
            return string.Empty;
        }

        // TODO: Use projectKey to make this dynamic
        var projectToken = "project";

        Debug.Assert(!projectToken.Contains("."), "Project token can't contain a dot or the GetRazorFilePath method will fail.");
        return "." + projectToken;
    }
}
