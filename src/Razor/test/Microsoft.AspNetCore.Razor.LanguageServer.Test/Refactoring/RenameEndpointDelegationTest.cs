// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Extensions;
using Microsoft.AspNetCore.Razor.LanguageServer.Test;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Workspaces.Extensions;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Refactoring
{
    [UseExportProvider]
    public class RenameEndpointDelegationTest: SingleServerDelegatingEndpointTestBase
    {
        [Fact]
        public async Task Handle_Rename_SingleServer_CSharpEditsAreMapped()
        {
            var input = """
                <div></div>

                @{
                    var $$myVariable = "Hello";

                    var length = myVariable.Length;
                }
                """;

            var newName = "newVar";

            var expected = """
                <div></div>

                @{
                    var newVar = "Hello";

                    var length = newVar.Length;
                }
                """;

            // Arrange
            TestFileMarkupParser.GetPosition(input, out var output, out var cursorPosition);
            var codeDocument = CreateCodeDocument(output);
            var razorFilePath = "C:/path/to/file.razor";

            await CreateLanguageServerAsync(codeDocument, razorFilePath);

            var projectSnapshotManager = Mock.Of<ProjectSnapshotManagerBase>(p => p.Projects == new[] { Mock.Of<ProjectSnapshot>(MockBehavior.Strict) }, MockBehavior.Strict);
            var projectSnapshotManagerAccessor = new TestProjectSnapshotManagerAccessor(projectSnapshotManager);
            var projectSnapshotManagerDispatcher = new LSPProjectSnapshotManagerDispatcher(LoggerFactory);
            var searchEngine = new DefaultRazorComponentSearchEngine(Dispatcher, projectSnapshotManagerAccessor, LoggerFactory);

            var endpoint = new RenameEndpoint(projectSnapshotManagerDispatcher, DocumentContextFactory, searchEngine, projectSnapshotManagerAccessor, LanguageServerFeatureOptions, DocumentMappingService, LanguageServer, TestLoggerFactory.Instance);

            codeDocument.GetSourceText().GetLineAndOffset(cursorPosition, out var line, out var offset);
            var request = new RenameParamsBridge
            {
                TextDocument = new TextDocumentIdentifier
                {
                    Uri = new Uri(razorFilePath)
                },
                Position = new Position(line, offset),
                NewName = newName
            };
            var requestContext = CreateRazorRequestContext(documentContext: null);

            // Act
            var result = await endpoint.HandleRequestAsync(request, requestContext, CancellationToken.None);

            // Assert
            var edits = result.DocumentChanges.Value.First.FirstOrDefault().Edits.Select(e => e.AsTextChange(codeDocument.GetSourceText()));
            var newText = codeDocument.GetSourceText().WithChanges(edits).ToString();
            Assert.Equal(expected, newText);
        }
    }
}
