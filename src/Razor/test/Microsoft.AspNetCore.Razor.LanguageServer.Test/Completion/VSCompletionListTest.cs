// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Linq;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    public class VSCompletionListTest
    {
        [Fact]
        public void Convert_DataTrue_RemovesDataFromItems()
        {
            // Arrange
            var dataObject = new JObject()
            {
                ["resultId"] = 123
            };
            var completionList = new CompletionList(
                new CompletionItem()
                {
                    Label = "Test",
                    Data = dataObject,
                });
            var capabilities = new VSCompletionListCapability()
            {
                Data = true,
            };

            // Act
            var vsCompletionList = VSCompletionList.Convert(completionList, capabilities);

            // Assert
            Assert.Collection(vsCompletionList.Items, item => Assert.Null(item.Data));
            Assert.Same(dataObject, vsCompletionList.Data);
        }

        [Fact]
        public void Convert_DataFalse_DoesNotTouchData()
        {
            // Arrange
            var dataObject = new JObject()
            {
                ["resultId"] = 123
            };
            var completionList = new CompletionList(
                new CompletionItem()
                {
                    Label = "Test",
                    Data = dataObject,
                });
            var capabilities = new VSCompletionListCapability()
            {
                Data = false,
            };

            // Act
            var vsCompletionList = VSCompletionList.Convert(completionList, capabilities);

            // Assert
            Assert.Collection(vsCompletionList.Items, item => Assert.Same(dataObject, item.Data));
            Assert.Null(vsCompletionList.Data);
        }

        [Fact]
        public void Convert_CommitCharactersTrue_RemovesCommitCharactersFromItems()
        {
            // Arrange
            var commitCharacters = new Container<string>("<");
            var completionList = new CompletionList(
                new CompletionItem()
                {
                    Label = "Test",
                    CommitCharacters = commitCharacters
                });
            var capabilities = new VSCompletionListCapability()
            {
                CommitCharacters = true,
            };

            // Act
            var vsCompletionList = VSCompletionList.Convert(completionList, capabilities);

            // Assert
            Assert.Collection(vsCompletionList.Items, (item) => Assert.Null(item.CommitCharacters));

            Assert.Collection(vsCompletionList.CommitCharacters, (e) =>
            {
                e.Insert = true;
                e.Character = "<";
            });
        }

        [Fact]
        public void Convert_CommitCharactersFalse_DoesNotTouchCommitCharacters()
        {
            // Arrange
            var commitCharacters = new Container<string>("<");
            var completionList = new CompletionList(
                new CompletionItem()
                {
                    Label = "Test",
                    CommitCharacters = commitCharacters
                });
            var capabilities = new VSCompletionListCapability()
            {
                CommitCharacters = false,
            };

            // Act
            var vsCompletionList = VSCompletionList.Convert(completionList, capabilities);

            // Assert
            Assert.Collection(vsCompletionList.Items, item => Assert.Equal(commitCharacters, item.CommitCharacters));
            Assert.Null(vsCompletionList.CommitCharacters);
        }
    }
}
