﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using Microsoft.VisualStudio.Text.Editor;
using Moq;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServer.ContainedLanguage
{
    public class DefaultFormattingOptionsProviderTest
    {
        [Fact]
        public void GetOptions_UsesIndentationManagerInformation()
        {
            // Arrange
            var documentManager = new TestDocumentManager();
            var documentUri = new Uri("C:/path/to/razorfile.razor");
            var documentSnapshot = new TestLSPDocumentSnapshot(documentUri, version: 0);
            documentManager.AddDocument(documentSnapshot.Uri, documentSnapshot);
            var expectedInsertSpaces = true;
            var expectedTabSize = 1337;
            var unneededIndentSize = 123;
            var indentationManagerService = new Mock<IIndentationManagerService>(MockBehavior.Strict);
            indentationManagerService
                .Setup(service => service.GetIndentation(documentSnapshot.Snapshot.TextBuffer, false, out expectedInsertSpaces, out expectedTabSize, out unneededIndentSize))
                .Verifiable();
            var provider = new DefaultFormattingOptionsProvider(documentManager, indentationManagerService.Object);

            // Act
            var options = provider.GetOptions(documentSnapshot);

            // Assert
            indentationManagerService.VerifyAll();
            Assert.Equal(expectedInsertSpaces, options.InsertSpaces);
            Assert.Equal(expectedTabSize, options.TabSize);
        }
    }
}
