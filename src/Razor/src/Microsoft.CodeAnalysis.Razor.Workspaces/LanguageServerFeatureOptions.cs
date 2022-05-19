// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Razor.Workspaces
{
    internal abstract class LanguageServerFeatureOptions
    {
        public abstract bool SupportsFileManipulation { get; }

        public abstract string ProjectConfigurationFileName { get; }

        public abstract string CSharpVirtualDocumentSuffix { get; }

        public abstract string HtmlVirtualDocumentSuffix { get; }

        public abstract bool SingleServerCompletionSupport { get; }
    }
}
