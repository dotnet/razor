// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using Microsoft.VisualStudio.LanguageServer.Protocol;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    public class CompletionListOptimizerTest
    {
        [Fact]
        public void Convert_DataTrue_RemovesDataFromItems()
        {
            // Arrange
            var dataObject = new JObject()
            {
                ["resultId"] = 123
            };
            var completionList = new VSInternalCompletionList()
            {
                Items = new[]
                {
                    new VSInternalCompletionItem()
                    {
                        Label = "Test",
                        Data = dataObject,
                    }
                }
            };
            var capabilities = new VSInternalCompletionSetting()
            {
                CompletionList = new VSInternalCompletionListSetting()
                {
                    Data = true,
                }
            };

            // Act
            var vsCompletionList = CompletionListOptimizer.Optimize(completionList, capabilities);

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
            var completionList = new VSInternalCompletionList()
            {
                Items = new[]
                {
                    new VSInternalCompletionItem()
                    {
                        Label = "Test",
                        Data = dataObject,
                    }
                }
            };
            var capabilities = new VSInternalCompletionSetting()
            {
                CompletionList = new VSInternalCompletionListSetting()
                {
                    Data = false,
                }
            };

            // Act
            var vsCompletionList = CompletionListOptimizer.Optimize(completionList, capabilities);

            // Assert
            Assert.Collection(vsCompletionList.Items, item => Assert.Same(dataObject, item.Data));
            Assert.Null(vsCompletionList.Data);
        }

        [Fact]
        public void Convert_CommitCharactersTrue_RemovesCommitCharactersFromItems()
        {
            // Arrange
            var commitCharacters = new[] { "<" };
            var completionList = new VSInternalCompletionList()
            {
                Items = new[]
                {
                    new VSInternalCompletionItem()
                    {
                        Label = "Test",
                        VsCommitCharacters = commitCharacters
                    }
                }
            };
            var capabilities = new VSInternalCompletionSetting()
            {
                CompletionList = new VSInternalCompletionListSetting()
                {
                    CommitCharacters = true,
                }
            };

            // Act
            var vsCompletionList = CompletionListOptimizer.Optimize(completionList, capabilities);

            // Assert
            Assert.Collection(vsCompletionList.Items, (item) => Assert.Null(item.CommitCharacters));

            Assert.Collection(vsCompletionList.CommitCharacters.Value.First, (e) => Assert.Equal("<", e));
        }

        [Fact]
        public void Convert_CommitCharactersFalse_DoesNotTouchCommitCharacters()
        {
            // Arrange
            var commitCharacters = new[] { "<" };
            var completionList = new VSInternalCompletionList()
            {
                Items = new[]
                {
                    new VSInternalCompletionItem()
                    {
                        Label = "Test",
                        VsCommitCharacters = commitCharacters
                    }
                }
            };
            var capabilities = new VSInternalCompletionSetting()
            {
                CompletionList = new VSInternalCompletionListSetting()
                {
                    CommitCharacters = false,
                }
            };

            // Act
            var vsCompletionList = CompletionListOptimizer.Optimize(completionList, capabilities);

            // Assert
            Assert.Collection(vsCompletionList.Items, item => Assert.Equal(commitCharacters, ((VSInternalCompletionItem)item).VsCommitCharacters));
            Assert.Null(vsCompletionList.CommitCharacters);
        }
    }
}
