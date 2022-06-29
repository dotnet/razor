// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Test;
using Microsoft.VisualStudio.Test;
using Microsoft.VisualStudio.Text;
using Moq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    public abstract class HandlerTestBase
    {
        public HandlerTestBase()
        {
            var logger = TestLogger.Instance;
            LoggerProvider = Mock.Of<HTMLCSharpLanguageServerLogHubLoggerProvider>(l =>
                l.CreateLogger(It.IsAny<string>()) == logger &&
                l.InitializeLoggerAsync(It.IsAny<CancellationToken>()) == Task.CompletedTask &&
                l.CreateLoggerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult<ILogger>(logger),
                MockBehavior.Strict);
        }

        internal HTMLCSharpLanguageServerLogHubLoggerProvider LoggerProvider { get; }

        internal static RazorCodeDocument CreateCodeDocument(
            string text,
            string filePath,
            params TagHelperDescriptor[] tagHelpers)
        {
            tagHelpers ??= Array.Empty<TagHelperDescriptor>();
            var sourceDocument = TestRazorSourceDocument.Create(text, filePath: filePath, relativePath: filePath);
            var projectEngine = RazorProjectEngine.Create(builder => { });
            var fileKind = filePath.EndsWith(".razor", StringComparison.Ordinal) ? FileKinds.Component : FileKinds.Legacy;
            var codeDocument = projectEngine.ProcessDesignTime(sourceDocument, fileKind, Array.Empty<RazorSourceDocument>(), tagHelpers);

            return codeDocument;
        }

        internal static CSharpVirtualDocumentSnapshot CreateCSharpVirtualDocumentSnapshot(
            RazorCodeDocument codeDocument,
            string virtualDocumentPath,
            long? hostDocumentSyncVersion = 1)
        {
            var textSnapshot = new StringTextSnapshot(codeDocument.GetCSharpDocument().GeneratedCode);
            textSnapshot.TextBuffer = new TestTextBuffer(textSnapshot);
            var virtualDocumentUri = new Uri(virtualDocumentPath);
            var virtualDocumentSnapshot = new CSharpVirtualDocumentSnapshot(virtualDocumentUri, textSnapshot, hostDocumentSyncVersion);

            return virtualDocumentSnapshot;
        }
    }
}
