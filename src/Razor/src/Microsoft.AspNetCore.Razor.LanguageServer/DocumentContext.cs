// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.LanguageServer
{
    internal record DocumentContext
    {
        public DocumentContext(
            Uri uri,
            RazorCodeDocument codeDocument,
            DocumentSnapshot snapshot,
            SourceText sourceText,
            int version)
        {
            Uri = uri;
            CodeDocument = codeDocument;
            Snapshot = snapshot;
            SourceText = sourceText;
            Version = version;
        }

        public Uri Uri { get; }

        public RazorCodeDocument CodeDocument { get; }

        public DocumentSnapshot Snapshot { get; }

        public SourceText SourceText { get; }

        public int Version { get; }

        public string FilePath => Snapshot.FilePath;

        public string FileKind => Snapshot.FileKind;

        public RazorSyntaxTree SyntaxTree => CodeDocument.GetSyntaxTree();

        public TagHelperDocumentContext TagHelperContext => CodeDocument.GetTagHelperContext();

        public SourceText CSharpSourceText => CodeDocument.GetCSharpSourceText();

        public SourceText HtmlSourceText => CodeDocument.GetHtmlSourceText();

        public ProjectSnapshot Project => Snapshot.Project;
    }
}
