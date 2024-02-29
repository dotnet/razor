﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.Extensions;

public class TemplateTargetExtensionTest
{
    [Fact]
    public void WriteTemplate_WritesTemplateCode()
    {
        // Arrange
        var node = new TemplateIntermediateNode()
        {
            Children =
                {
                    new CSharpExpressionIntermediateNode()
                }
        };
        var extension = new TemplateTargetExtension()
        {
            TemplateTypeName = "global::TestTemplate"
        };

        var nodeWriter = new RuntimeNodeWriter()
        {
            PushWriterMethod = "TestPushWriter",
            PopWriterMethod = "TestPopWriter"
        };

        using var context = TestCodeRenderingContext.CreateRuntime(nodeWriter: nodeWriter);

        // Act
        extension.WriteTemplate(context, node);

        // Assert
        var expected = @"item => new global::TestTemplate(async(__razor_template_writer) => {
    TestPushWriter(__razor_template_writer);
    Render Children
    TestPopWriter();
}
)";

        var output = context.CodeWriter.GenerateCode();
        Assert.Equal(expected, output);
    }
}
