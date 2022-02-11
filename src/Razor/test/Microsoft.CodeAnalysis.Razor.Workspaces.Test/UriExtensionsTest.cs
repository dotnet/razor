// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor;
using Xunit;

namespace Microsoft.CodeAnalysis.Razor
{
    public class UriExtensionsTest
    {
        [OSSkipConditionFact(new[] { "OSX", "Linux" })]
        public void GetAbsoluteOrUNCPath_ReturnsAbsolutePath()
        {
            // Arrange
            var uri = new Uri("c:\\Some\\path\\to\\file.cshtml");

            // Act
            var path = uri.GetAbsoluteOrUNCPath();

            // Assert
            Assert.Equal(uri.AbsolutePath, path);
        }

        [Fact]
        public void GetAbsoluteOrUNCPath_UNCPath_ReturnsLocalPath()
        {
            // Arrange
            var uri = new Uri("//Some/path/to/file.cshtml");

            // Act
            var path = uri.GetAbsoluteOrUNCPath();

            // Assert
            Assert.Equal(uri.LocalPath, path);
        }
    }
}
