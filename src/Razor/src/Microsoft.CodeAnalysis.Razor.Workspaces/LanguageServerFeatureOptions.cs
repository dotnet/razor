// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;

namespace Microsoft.CodeAnalysis.Razor.Workspaces
{
    internal abstract class LanguageServerFeatureOptions
    {
        public abstract bool SupportsFileManipulation { get; }

        public abstract string ProjectConfigurationFileName { get; }

        public abstract string CSharpVirtualDocumentSuffix { get; }

        public abstract string HtmlVirtualDocumentSuffix { get; }

        public abstract bool SingleServerCompletionSupport { get; }

        public abstract bool SingleServerSupport { get; }

        /// <summary>
        /// This is a temporary workaround to account for the fact that both O# and Razor register handlers.
        /// It can be removed once we switch over to CLaSP:
        /// https://github.com/dotnet/razor-tooling/issues/6871
        /// </summary>
        public abstract bool RegisterBuiltInFeatures { get; }

        public string GetRazorCSharpFilePath(string razorFilePath) => razorFilePath + CSharpVirtualDocumentSuffix;

        public string GetRazorHtmlFilePath(string razorFilePath) => razorFilePath + HtmlVirtualDocumentSuffix;

        public string GetRazorFilePath(string filePath)
        {
            filePath = filePath.Replace(CSharpVirtualDocumentSuffix, string.Empty);
            filePath = filePath.Replace(HtmlVirtualDocumentSuffix, string.Empty);

            return filePath;
        }

        public Uri GetRazorDocumentUri(Uri virtualDocumentUri)
        {
            var uriPath = virtualDocumentUri.AbsoluteUri;
            var razorFilePath = GetRazorFilePath(uriPath);
            var uri = new Uri(razorFilePath, UriKind.Absolute);
            return uri;
        }

        public bool IsVirtualCSharpFile(Uri uri)
            => CheckIfFileUriAndExtensionMatch(uri, CSharpVirtualDocumentSuffix);

        public bool IsVirtualHtmlFile(Uri uri)
            => CheckIfFileUriAndExtensionMatch(uri, HtmlVirtualDocumentSuffix);

        public bool IsVirtualDocumentUri(Uri uri)
            => IsVirtualCSharpFile(uri) || IsVirtualHtmlFile(uri);

        private static bool CheckIfFileUriAndExtensionMatch(Uri uri, string extension)
            => uri.GetAbsoluteOrUNCPath()?.EndsWith(extension, StringComparison.Ordinal) ?? false;
    }
}
