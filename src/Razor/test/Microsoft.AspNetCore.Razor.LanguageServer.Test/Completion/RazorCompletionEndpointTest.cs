// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.LanguageServer.EndpointContracts;
using Microsoft.AspNetCore.Razor.LanguageServer.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    public class RazorCompletionEndpointTest : LanguageServerTestBase
    {
        [Fact]
        public async Task Handle_NoDocumentContext_NoCompletionItems()
        {
            // Arrange
            var documentPath = "C:/path/to/document.cshtml";
            var documentContextFactory = new TestDocumentContextFactory();
            var completionEndpoint = new RazorCompletionEndpoint(
                documentContextFactory, null, LoggerFactory);
            var request = new VSCompletionParamsBridge()
            {
                TextDocument = new TextDocumentIdentifier()
                {
                    Uri = new Uri(documentPath)
                },
                Position = new Position(0, 1),
                Context = new VSInternalCompletionContext(),
            };

            // Act
            var completionList = await Task.Run(() => completionEndpoint.Handle(request, default));

            // Assert
            Assert.Null(completionList);
        }
    }
}
