// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Composition;

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

    public string GetRazorCSharpFilePath(string razorFilePath) => razorFilePath + _languageServerFeatureOptions.CSharpVirtualDocumentSuffix;

    public string GetRazorHtmlFilePath(string razorFilePath) => razorFilePath + _languageServerFeatureOptions.HtmlVirtualDocumentSuffix;

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
        filePath = filePath.Replace(_languageServerFeatureOptions.CSharpVirtualDocumentSuffix, string.Empty);
        filePath = filePath.Replace(_languageServerFeatureOptions.HtmlVirtualDocumentSuffix, string.Empty);

        return filePath;
    }
}
