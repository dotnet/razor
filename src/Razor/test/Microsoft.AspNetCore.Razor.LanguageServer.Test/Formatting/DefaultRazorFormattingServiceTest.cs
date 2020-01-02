// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;
using Xunit;
using Microsoft.CodeAnalysis.Razor;
using Moq;
using Microsoft.AspNetCore.Razor.LanguageServer.Common;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Formatting
{
    public class DefaultRazorFormattingServiceTest
    {
        [Fact]
        public async Task FormatAsync_FormatsDocument()
        {
            // Arrange
            var path = "file:///path/to/document.razor";
            var uri = new Uri(path);
            var source = SourceText.From(@"
@{
var x = ""foo"";
}
<div>
<span>
                Hello
 </span>
        </div>
");
            var codeDocument = CreateCodeDocument(source, path);
            var range = new Range(new Position(0, 0), new Position(source.Lines.Count-1, 0));
            var options = new FormattingOptions()
            {
                TabSize = 2,
                InsertSpaces = true,
            };
            var formattingService = CreateService();

            // Act
            var edits = await formattingService.FormatAsync(uri, codeDocument, range, options);

            // Assert
            var edited = ApplyEdits(source, edits);
            Assert.Equal(@"
@{
  var x = ""foo"";
}
<div>
  <span>
    Hello
  </span>
</div>
", edited.ToString());
        }

        private SourceText ApplyEdits(SourceText source, TextEdit[] edits)
        {
            var changes = edits.Select(e => e.AsTextChange(source));
            return source.WithChanges(changes);
        }

        private static RazorCodeDocument CreateCodeDocument(SourceText text, string path, IReadOnlyList<TagHelperDescriptor> tagHelpers = null)
        {
            tagHelpers = tagHelpers ?? Array.Empty<TagHelperDescriptor>();
            var sourceDocument = text.GetRazorSourceDocument(path, path);
            var projectEngine = RazorProjectEngine.Create(builder => { });
            var codeDocument = projectEngine.ProcessDesignTime(sourceDocument, FileKinds.Legacy, Array.Empty<RazorSourceDocument>(), tagHelpers);
            return codeDocument;
        }

        private RazorFormattingService CreateService()
        {
            var foregroundDispatcher = Mock.Of<ForegroundDispatcher>();
            var mappingService = new DefaultRazorDocumentMappingService();
            var filePathNormalizer = new FilePathNormalizer();
            var projectSnapshotManagerAccessor = Mock.Of<ProjectSnapshotManagerAccessor>();
            var languageServer = Mock.Of<ILanguageServer>();

            return new DefaultRazorFormattingService(foregroundDispatcher, mappingService, filePathNormalizer, projectSnapshotManagerAccessor, languageServer);
        }
    }
}
