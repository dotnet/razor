// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.Language.Legacy;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

public class KeyedInjectTargetExtensionTest
{
    
    [Fact]
    public void KeyedInjectDirectiveTargetExtension_WritesProperty()
    {
        // Arrange
        using var context = TestCodeRenderingContext.CreateRuntime();
        var target = new KeyedInjectTargetExtension(considerNullabilityEnforcement: true);
        var node = new KeyedInjectIntermediateNode()
        {
            TypeName = "PropertyType",
            MemberName = "PropertyName",
            KeyName = "\"PropertyKey\"",
        };

        // Act
        target.WriteKeyedInjectProperty(context, node);

        // Assert
        Assert.Equal("""
            #nullable restore
            [global::Microsoft.AspNetCore.Mvc.Razor.Internal.RazorInjectAttribute(Key = "PropertyKey")]
            public PropertyType PropertyName { get; private set; } = default!;
            #nullable disable

            """,
            context.CodeWriter.GetText().ToString());
    }

}
