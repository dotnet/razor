// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.CodeAnalysis.Razor.Tooltip
{
    public class BoundAttributeDescriptionInfoTest
    {
        [Fact]
        public void ResolveTagHelperTypeName_ExtractsTypeName_SimpleReturnType()
        {
            // Arrange & Act
            var typeName = BoundAttributeDescriptionInfo.ResolveTagHelperTypeName("System.String", "SomePropertyName", "string SomeTypeName.SomePropertyName");

            // Assert
            Assert.Equal("SomeTypeName", typeName);
        }

        [Fact]
        public void ResolveTagHelperTypeName_ExtractsTypeName_ComplexReturnType()
        {
            // Arrange & Act
            var typeName = BoundAttributeDescriptionInfo.ResolveTagHelperTypeName("SomeReturnTypeName", "SomePropertyName", "SomeReturnTypeName SomeTypeName.SomePropertyName");

            // Assert
            Assert.Equal("SomeTypeName", typeName);
        }
    }
}
