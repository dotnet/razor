// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.AspNetCore.Razor.Language.Extensions
{
    public static class MvcImportItems
    {
        private const string ImportsFileName = "_ViewImports.cshtml";
        private const string DefaultImport =
@"
@using System
@using System.Collections.Generic
@using System.Linq
@using System.Threading.Tasks
@using Microsoft.AspNetCore.Mvc
@using Microsoft.AspNetCore.Mvc.Rendering
@using Microsoft.AspNetCore.Mvc.ViewFeatures
@inject global::Microsoft.AspNetCore.Mvc.Rendering.IHtmlHelper<TModel> Html
@inject global::Microsoft.AspNetCore.Mvc.Rendering.IJsonHelper Json
@inject global::Microsoft.AspNetCore.Mvc.IViewComponentHelper Component
@inject global::Microsoft.AspNetCore.Mvc.IUrlHelper Url
@inject global::Microsoft.AspNetCore.Mvc.ViewFeatures.IModelExpressionProvider ModelExpressionProvider
@addTagHelper Microsoft.AspNetCore.Mvc.Razor.TagHelpers.UrlResolutionTagHelper, Microsoft.AspNetCore.Mvc.Razor
@addTagHelper Microsoft.AspNetCore.Mvc.Razor.TagHelpers.HeadTagHelper, Microsoft.AspNetCore.Mvc.Razor
@addTagHelper Microsoft.AspNetCore.Mvc.Razor.TagHelpers.BodyTagHelper, Microsoft.AspNetCore.Mvc.Razor
";

        public static IReadOnlyList<RazorSourceDocument> GetImportDocuments(
            RazorProjectFileSystem projectFileSystem,
            RazorProjectItem projectItem)
        {
            if (projectFileSystem == null)
            {
                throw new ArgumentNullException(nameof(projectFileSystem));
            }

            if (projectItem == null)
            {
                throw new ArgumentNullException(nameof(projectItem));
            }

            var imports = new List<RazorSourceDocument>
            {
                RazorSourceDocument.Create(DefaultImport, fileName: null),
            };

            // We want items in descending order. FindHierarchicalItems returns items in ascending order.
            foreach (var importItem in projectFileSystem.FindHierarchicalItems(projectItem.FilePath, ImportsFileName).Reverse())
            {
                if (importItem.Exists)
                {
                    imports.Add(RazorSourceDocument.ReadFrom(importItem));
                }
            }

            return imports;
        }

        public static IReadOnlyList<RazorProjectItem> GetImportItems(
            RazorProjectFileSystem projectFileSystem,
            RazorProjectItem projectItem)
        {
            if (projectFileSystem == null)
            {
                throw new ArgumentNullException(nameof(projectFileSystem));
            }

            if (projectItem == null)
            {
                throw new ArgumentNullException(nameof(projectItem));
            }

            var imports = new List<RazorProjectItem>
            {
                DefaultDirectivesProjectItem.Instance,
            };

            // We want items in descending order. FindHierarchicalItems returns items in ascending order.
            foreach (var importItem in projectFileSystem.FindHierarchicalItems(projectItem.FilePath, ImportsFileName).Reverse())
            {
                imports.Add(importItem);
            }

            return imports;
        }

        private class DefaultDirectivesProjectItem : RazorProjectItem
        {
            private readonly byte[] _defaultImportBytes;

            private DefaultDirectivesProjectItem()
            {
                var preamble = Encoding.UTF8.GetPreamble();

                var contentBytes = Encoding.UTF8.GetBytes(DefaultImport);

                _defaultImportBytes = new byte[preamble.Length + contentBytes.Length];
                preamble.CopyTo(_defaultImportBytes, 0);
                contentBytes.CopyTo(_defaultImportBytes, preamble.Length);
            }

            public override string BasePath => null;

            public override string FilePath => null;

            public override string PhysicalPath => null;

            public override bool Exists => true;

            public static DefaultDirectivesProjectItem Instance { get; } = new DefaultDirectivesProjectItem();

            public override Stream Read() => new MemoryStream(_defaultImportBytes);
        }
    }
}
