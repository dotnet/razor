﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

#nullable disable

using System.Linq;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.CodeAnalysis.Razor.Serialization;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.VisualStudio.LanguageServices.Razor.Serialization
{
    public class RazorExtensionSerializationTest
    {
        public RazorExtensionSerializationTest()
        {
            var converters = new JsonConverterCollection
            {
                RazorExtensionJsonConverter.Instance
            };
            Converters = converters.ToArray();
        }

        private JsonConverter[] Converters { get; }

        [Fact]
        public void RazorExensionJsonConverter_Serialization_CanRoundTrip()
        {
            // Arrange
            var extension = new ProjectSystemRazorExtension("Test");

            // Act
            var json = JsonConvert.SerializeObject(extension, Converters);
            var obj = JsonConvert.DeserializeObject<RazorExtension>(json, Converters);

            // Assert
            Assert.Equal(extension.ExtensionName, obj.ExtensionName);
        }
    }
}
