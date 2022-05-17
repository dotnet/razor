// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using Microsoft.CodeAnalysis.Razor.Workspaces;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal class TestLanguageServerFeatureOptions : LanguageServerFeatureOptions
    {
        public static readonly LanguageServerFeatureOptions Instance = new TestLanguageServerFeatureOptions();

        private TestLanguageServerFeatureOptions()
        {
        }

        public override bool SupportsFileManipulation => false;

        public override string ProjectConfigurationFileName => LanguageServerConstants.DefaultProjectConfigurationFile;

        public override string CSharpVirtualDocumentSuffix => ".g.cs";

        public override string HtmlVirtualDocumentSuffix => "__virtual.html";
    }
}
