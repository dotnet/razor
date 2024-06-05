// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Razor.ProjectSystem;

namespace Microsoft.CodeAnalysis.Razor.Workspaces;

internal abstract class AbstractFilePathService(ILanguageServerFeatureOptionsProvider optionsProvider) : IFilePathService
{
    private readonly Lazy<LanguageServerFeatureOptions> _lazyOptions = new(optionsProvider.GetOptions);

    private LanguageServerFeatureOptions Options => _lazyOptions.Value;

    public string GetRazorCSharpFilePath(ProjectKey projectKey, string razorFilePath)
        => GetGeneratedFilePath(projectKey, razorFilePath, Options.CSharpVirtualDocumentSuffix);

    public Uri GetRazorDocumentUri(Uri virtualDocumentUri)
    {
        var uriPath = virtualDocumentUri.AbsoluteUri;
        var razorFilePath = GetRazorFilePath(uriPath);
        var uri = new Uri(razorFilePath, UriKind.Absolute);
        return uri;
    }

    public bool IsVirtualCSharpFile(Uri uri)
        => CheckIfFileUriAndExtensionMatch(uri, Options.CSharpVirtualDocumentSuffix);

    public bool IsVirtualHtmlFile(Uri uri)
        => CheckIfFileUriAndExtensionMatch(uri, Options.HtmlVirtualDocumentSuffix);

    public bool IsVirtualDocumentUri(Uri uri)
        => IsVirtualCSharpFile(uri) || IsVirtualHtmlFile(uri);

    private static bool CheckIfFileUriAndExtensionMatch(Uri uri, string extension)
        => uri.GetAbsoluteOrUNCPath()?.EndsWith(extension, StringComparison.Ordinal) ?? false;

    private string GetRazorFilePath(string filePath)
    {
        var trimIndex = filePath.LastIndexOf(Options.CSharpVirtualDocumentSuffix);
        if (trimIndex == -1)
        {
            trimIndex = filePath.LastIndexOf(Options.HtmlVirtualDocumentSuffix);
        }
        else if (Options.IncludeProjectKeyInGeneratedFilePath)
        {
            // If this is a C# generated file, and we're including the project suffix, then filename will be
            // <Page>.razor.<project slug><c# suffix>
            // This means we can remove the project key easily, by just looking for the last '.'. The project
            // slug itself cannot a '.', enforced by the assert below in GetProjectSuffix

            trimIndex = filePath.LastIndexOf('.', trimIndex - 1);
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

    private string GetProjectSuffix(ProjectKey projectKey)
    {
        if (!Options.IncludeProjectKeyInGeneratedFilePath)
        {
            return string.Empty;
        }

        // If there is no project key, we still want to generate something as otherwise the GetRazorFilePath method
        // would end up unnecessarily overcomplicated
        if (projectKey.IsUnknown)
        {
            return ".p";
        }

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var crypto = sha256.ComputeHash(Encoding.Unicode.GetBytes(projectKey.Id));
        var projectToken = Convert.ToBase64String(crypto, 0, 12).Replace('/', '_').Replace('+', '-');

        Debug.Assert(!projectToken.Contains("."), "Project token can't contain a dot or the GetRazorFilePath method will fail.");
        return "." + projectToken;
    }
}
