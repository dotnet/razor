// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Logging;
using Microsoft.VisualStudio.LanguageServerClient.Razor.Test;
using Microsoft.VisualStudio.Text;
using Moq;

namespace Microsoft.VisualStudio.LanguageServerClient.Razor.HtmlCSharp
{
    [UseExportProvider]
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

        internal static async Task<TResponse> ExecuteCSharpRequestAsync<TRequest, TResponse>(
            RazorCodeDocument codeDocument,
            Uri csharpDocumentUri,
            ServerCapabilities serverCapabilities,
            TRequest requestParams,
            string methodName,
            CancellationToken cancellationToken) where TRequest : class
        {
            var csharpSourceText = codeDocument.GetCSharpSourceText();
            var files = new List<(Uri, SourceText)>
            {
                (csharpDocumentUri, csharpSourceText)
            };

            var exportProvider = RoslynTestCompositions.Roslyn.ExportProviderFactory.CreateExportProvider();
            using var workspace = CSharpTestLspServerHelpers.CreateCSharpTestWorkspace(files, exportProvider);
            await using var csharpLspServer = await CSharpTestLspServerHelpers.CreateCSharpLspServerAsync(workspace, exportProvider, serverCapabilities);

            var result = await csharpLspServer.ExecuteRequestAsync<TRequest, TResponse>(
                methodName,
                requestParams,
                cancellationToken).ConfigureAwait(false);

            return result;
        }

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
            string virtualDocumentPath)
        {
            var textSnapshot = new StringTextSnapshot(codeDocument.GetCSharpDocument().GeneratedCode);
            var virtualDocumentUri = new Uri(virtualDocumentPath);
            var virtualDocumentSnapshot = new CSharpVirtualDocumentSnapshot(virtualDocumentUri, textSnapshot, hostDocumentSyncVersion: 1);

            return virtualDocumentSnapshot;
        }
    }
}
