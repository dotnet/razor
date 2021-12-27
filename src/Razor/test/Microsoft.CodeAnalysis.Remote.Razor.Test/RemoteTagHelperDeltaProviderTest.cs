// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Remote.Razor.Test;
using Xunit;

namespace Microsoft.CodeAnalysis.Remote.Razor
{
    public class RemoteTagHelperDeltaProviderTest : TagHelperDescriptorTestBase
    {
        public RemoteTagHelperDeltaProviderTest()
        {
            Provider = new RemoteTagHelperDeltaProvider();
        }

        private RemoteTagHelperDeltaProvider Provider { get; }

        [Fact]
        public void GetTagHelpersDelta_Clean_SingleProject()
        {
            // Act
            var delta = Provider.GetTagHelpersDelta(Project1FilePath, lastResultId: -1, Project1TagHelpers);

            // Assert
            Assert.False(delta.Delta);
            Assert.Equal(Project1TagHelpers, delta.Added);
            Assert.Empty(delta.Removed);
        }

        [Fact]
        public void GetTagHelpersDelta_Clean_MultiProject()
        {
            // Act
            var delta1 = Provider.GetTagHelpersDelta(Project1FilePath, lastResultId: -1, Project1TagHelpers);
            var delta2 = Provider.GetTagHelpersDelta(Project2FilePath, lastResultId: -1, Project2TagHelpers);

            // Assert
            Assert.False(delta1.Delta);
            Assert.Equal(Project1TagHelpers, delta1.Added);
            Assert.Empty(delta1.Removed);
            Assert.False(delta2.Delta);
            Assert.Equal(Project2TagHelpers, delta2.Added);
            Assert.Empty(delta2.Removed);
        }

        [Fact]
        public void GetTagHelpersDelta_TagHelperRemovedFromProjectOne_InvalidResultId()
        {
            // Arrange
            var tagHelpersWithOneRemoved = new[]
            {
                TagHelper1_Project1
            };
            Provider.GetTagHelpersDelta(Project1FilePath, lastResultId: -1, Project1TagHelpers);
            Provider.GetTagHelpersDelta(Project2FilePath, lastResultId: -1, Project2TagHelpers);

            // Act
            var delta = Provider.GetTagHelpersDelta(Project1FilePath, lastResultId: -1337, tagHelpersWithOneRemoved);

            // Assert
            Assert.False(delta.Delta);
            Assert.Equal(tagHelpersWithOneRemoved, delta.Added);
            Assert.Empty(delta.Removed);
        }

        [Fact]
        public void GetTagHelpersDelta_TagHelperRemovedFromProjectOne()
        {
            // Arrange
            var tagHelpersWithOneRemoved = new[]
            {
                TagHelper1_Project1
            };
            var initialDelta = Provider.GetTagHelpersDelta(Project1FilePath, lastResultId: -1, Project1TagHelpers);
            Provider.GetTagHelpersDelta(Project2FilePath, lastResultId: -1, Project2TagHelpers);

            // Act
            var delta = Provider.GetTagHelpersDelta(Project1FilePath, initialDelta.ResultId, tagHelpersWithOneRemoved);

            // Assert
            Assert.True(delta.Delta);
            Assert.Empty(delta.Added);
            var tagHelper = Assert.Single(delta.Removed);
            Assert.Equal(TagHelper2_Project1, tagHelper);
        }

        [Fact]
        public void GetTagHelpersDelta_TagHelpersCopiedToProjectOne()
        {
            // Arrange
            var tagHelpers = new List<TagHelperDescriptor>();
            tagHelpers.AddRange(Project1TagHelpers);
            tagHelpers.AddRange(Project2TagHelpers);
            var initialDelta = Provider.GetTagHelpersDelta(Project1FilePath, lastResultId: -1, Project1TagHelpers);
            Provider.GetTagHelpersDelta(Project2FilePath, lastResultId: -1, Project2TagHelpers);

            // Act
            var delta = Provider.GetTagHelpersDelta(Project1FilePath, initialDelta.ResultId, tagHelpers);

            // Assert
            Assert.True(delta.Delta);
            Assert.Equal(Project2TagHelpers, delta.Added);
            Assert.Empty(delta.Removed);
        }

        [Fact]
        public void GetTagHelpersDelta_NoChange()
        {
            // Arrange
            var initialDelta = Provider.GetTagHelpersDelta(Project1FilePath, lastResultId: -1, Project1TagHelpers);

            // Act
            var delta = Provider.GetTagHelpersDelta(Project1FilePath, initialDelta.ResultId, Project1TagHelpers);

            // Assert
            Assert.True(delta.Delta);
            Assert.Empty(delta.Added);
            Assert.Empty(delta.Removed);
        }

        [Fact]
        public void GetTagHelpersDelta_EndToEnd()
        {
            // Arrange
            var mixedTagHelpers1 = new[]
            {
                TagHelper1_Project1,
                TagHelper1_Project2,
            };
            var mixedTagHelpers2 = new[]
            {
                TagHelper2_Project1,
                TagHelper2_Project2,
            };
            var initialDelta1 = Provider.GetTagHelpersDelta(Project1FilePath, lastResultId: -1, Project1TagHelpers);
            var initialDelta2 = Provider.GetTagHelpersDelta(Project2FilePath, lastResultId: -1, Project2TagHelpers);

            // Act - 1
            var delta1 = Provider.GetTagHelpersDelta(Project1FilePath, initialDelta1.ResultId, mixedTagHelpers1);
            var delta2 = Provider.GetTagHelpersDelta(Project2FilePath, initialDelta2.ResultId, mixedTagHelpers2);

            // Assert - 1
            Assert.True(delta1.Delta);
            Assert.Equal(new[] { TagHelper1_Project2 }, delta1.Added);
            Assert.Equal(new[] { TagHelper2_Project1 }, delta1.Removed);
            Assert.True(delta2.Delta);
            Assert.Equal(new[] { TagHelper2_Project1 }, delta2.Added);
            Assert.Equal(new[] { TagHelper1_Project2 }, delta2.Removed);

            // Act - 2 (restore to original state)
            delta1 = Provider.GetTagHelpersDelta(Project1FilePath, delta1.ResultId, Project1TagHelpers);
            delta2 = Provider.GetTagHelpersDelta(Project2FilePath, delta2.ResultId, Project2TagHelpers);

            // Assert - 2
            Assert.True(delta1.Delta);
            Assert.Equal(new[] { TagHelper2_Project1 }, delta1.Added);
            Assert.Equal(new[] { TagHelper1_Project2 }, delta1.Removed);
            Assert.True(delta2.Delta);
            Assert.Equal(new[] { TagHelper1_Project2 }, delta2.Added);
            Assert.Equal(new[] { TagHelper2_Project1 }, delta2.Removed);

            // Act - 3 (No-op)
            delta1 = Provider.GetTagHelpersDelta(Project1FilePath, delta1.ResultId, Project1TagHelpers);
            delta2 = Provider.GetTagHelpersDelta(Project2FilePath, delta2.ResultId, Project2TagHelpers);

            // Assert - 3
            Assert.True(delta1.Delta);
            Assert.Empty(delta1.Added);
            Assert.Empty(delta1.Removed);
            Assert.True(delta2.Delta);
            Assert.Empty(delta2.Added);
            Assert.Empty(delta2.Removed);
        }
    }
}
