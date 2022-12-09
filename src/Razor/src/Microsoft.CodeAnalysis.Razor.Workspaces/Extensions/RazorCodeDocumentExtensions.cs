// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Razor.Workspaces.Extensions
{
    internal static class RazorCodeDocumentExtensions
    {
        private static readonly object s_sourceTextKey = new();
        private static readonly object s_cSharpSourceTextKey = new();
        private static readonly object s_htmlSourceTextKey = new();

        public static SourceText GetSourceText(this RazorCodeDocument document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var sourceTextObj = document.Items[s_sourceTextKey];
            if (sourceTextObj is null)
            {
                var source = document.Source;
                var charBuffer = new char[source.Length];
                source.CopyTo(0, charBuffer, 0, source.Length);
                var sourceText = SourceText.From(new string(charBuffer));
                document.Items[s_sourceTextKey] = sourceText;

                return sourceText;
            }

            return (SourceText)sourceTextObj;
        }

        public static SourceText GetCSharpSourceText(this RazorCodeDocument document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var sourceTextObj = document.Items[s_cSharpSourceTextKey];
            if (sourceTextObj is null)
            {
                var csharpDocument = document.GetCSharpDocument();
                var sourceText = SourceText.From(csharpDocument.GeneratedCode);
                document.Items[s_cSharpSourceTextKey] = sourceText;

                return sourceText;
            }

            return (SourceText)sourceTextObj;
        }

        public static SourceText GetHtmlSourceText(this RazorCodeDocument document)
        {
            if (document is null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var sourceTextObj = document.Items[s_htmlSourceTextKey];
            if (sourceTextObj is null)
            {
                var htmlDocument = document.GetHtmlDocument();
                var sourceText = SourceText.From(htmlDocument.GeneratedHtml);
                document.Items[s_htmlSourceTextKey] = sourceText;

                return sourceText;
            }

            return (SourceText)sourceTextObj;
        }
    }
}
