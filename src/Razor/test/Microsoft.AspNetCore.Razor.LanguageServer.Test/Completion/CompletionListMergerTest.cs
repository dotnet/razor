// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Linq;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Xunit;

namespace Microsoft.AspNetCore.Razor.LanguageServer.Completion
{
    public class CompletionListMergerTest
    {
        public CompletionListMergerTest()
        {
            var completionItem1 = new VSInternalCompletionItem()
            {
                Label = "CompletionItem1"
            };
            var completionItem2 = new VSInternalCompletionItem()
            {
                Label = "CompletionItem2"
            };
            var completionItem3 = new VSInternalCompletionItem()
            {
                Label = "CompletionItem3"
            };
            var completionListWith1 = new VSInternalCompletionList()
            {
                Items = new[] { completionItem1 }
            };
            var completionListWith2 = new VSInternalCompletionList()
            {
                Items = new[] { completionItem2 }
            };
            var completionListWith13 = new VSInternalCompletionList()
            {
                Items = new[] { completionItem1, completionItem3 }
            };

            CompletionItem1 = completionItem1;
            CompletionItem2 = completionItem2;
            CompletionItem3 = completionItem3;
            CompletionListWith1 = completionListWith1;
            CompletionListWith2 = completionListWith2;
            CompletionListWith13 = completionListWith13;
        }

        private VSInternalCompletionItem CompletionItem1 { get; }

        private VSInternalCompletionItem CompletionItem2 { get; }

        private VSInternalCompletionItem CompletionItem3 { get; }

        private VSInternalCompletionList CompletionListWith1 { get; }

        private VSInternalCompletionList CompletionListWith2 { get; }

        private VSInternalCompletionList CompletionListWith13 { get; }

        [Fact]
        public void Merge_FirstCompletionListNull_ReturnsSecond()
        {
            // Arrange

            // Act
            var merged = CompletionListMerger.Merge(completionListA: null, CompletionListWith1);

            // Assert
            Assert.Same(merged, CompletionListWith1);
        }

        [Fact]
        public void Merge_SecondCompletionListNull_ReturnsFirst()
        {
            // Arrange

            // Act
            var merged = CompletionListMerger.Merge(CompletionListWith1, completionListB: null);

            // Assert
            Assert.Same(merged, CompletionListWith1);
        }

        [Fact]
        public void Merge_RepresentsAllItems()
        {
            // Arrange
            var expected = new[] { CompletionItem1, CompletionItem2 };

            // Act
            var merged = CompletionListMerger.Merge(CompletionListWith1, CompletionListWith2);

            // Assert
            AssertCompletionItemsEqual(expected, merged.Items);
        }

        [Fact]
        public void Merge_RepresentsIsIncompleteOfBothLists()
        {
            // Arrange
            CompletionListWith1.IsIncomplete = false;
            CompletionListWith2.IsIncomplete = true;

            // Act
            var merged = CompletionListMerger.Merge(CompletionListWith1, CompletionListWith2);

            // Assert
            Assert.True(merged.IsIncomplete);
        }

        [Fact]
        public void Merge_RepresentsSuggestionModeOfBothLists()
        {
            // Arrange
            CompletionListWith1.SuggestionMode = false;
            CompletionListWith2.SuggestionMode = true;

            // Act
            var merged = CompletionListMerger.Merge(CompletionListWith1, CompletionListWith2);

            // Assert
            Assert.True(merged.SuggestionMode);
        }

        [Fact]
        public void Merge_CommitCharacters_OneInherits()
        {
            // Arrange
            var expectedCommitCharacters = new string[] { " " };
            CompletionListWith1.CommitCharacters = expectedCommitCharacters;

            // Act
            var merged = CompletionListMerger.Merge(CompletionListWith1, CompletionListWith2);

            // Assert
            Assert.Equal(expectedCommitCharacters, merged.CommitCharacters);
        }

        [Fact]
        public void Merge_CommitCharacters_BothInherit_ChoosesMoreImpactfulList()
        {
            // Arrange
            var lesserCommitCharacters = new string[] { " " };
            CompletionListWith2.CommitCharacters = lesserCommitCharacters;
            var expectedCommitCharacters = new string[] { ".", ">" };
            CompletionListWith13.CommitCharacters = expectedCommitCharacters;

            // Act
            var merged = CompletionListMerger.Merge(CompletionListWith2, CompletionListWith13);

            // Assert
            Assert.Equal(expectedCommitCharacters, merged.CommitCharacters);

            // Inherited commit characters got populated onto the non-chosen item.
            Assert.Equal(CompletionItem2.VsCommitCharacters, lesserCommitCharacters);
            Assert.Null(CompletionItem3.VsCommitCharacters);
        }

        [Fact]
        public void Merge_Data_OneInherits()
        {
            // Arrange
            var expectedData = new object();
            CompletionListWith1.Data = expectedData;

            // Act
            var merged = CompletionListMerger.Merge(CompletionListWith1, CompletionListWith2);

            // Assert
            Assert.Same(expectedData, merged.Data);
            Assert.NotNull(CompletionItem2.Data);
            Assert.NotSame(expectedData, CompletionItem2.Data);
        }

        [Fact]
        public void Merge_Data_BothInherit_ChoosesMoreImpactfulList()
        {
            // Arrange
            var data1 = new object();
            CompletionListWith1.Data = data1;
            var data2 = new object();
            CompletionListWith2.Data = data2;

            // Act
            var merged = CompletionListMerger.Merge(CompletionListWith1, CompletionListWith2);

            // Assert
            Assert.NotSame(data1, merged.Data);
            Assert.NotSame(data2, merged.Data);
        }

        private void AssertCompletionItemsEqual(VSInternalCompletionItem[] expected, CompletionItem[] actual)
        {
            var sortedExpected = expected.OrderBy(item => item.Label).ToArray();
            var sortedActual = actual.OrderBy(item => item.Label).ToArray();

            Assert.Equal(sortedExpected.Length, sortedActual.Length);

            for (var i = 0; i < sortedExpected.Length; i++)
            {
                Assert.Same(sortedExpected[i], sortedActual[i]);
            }
        }

    }
}
