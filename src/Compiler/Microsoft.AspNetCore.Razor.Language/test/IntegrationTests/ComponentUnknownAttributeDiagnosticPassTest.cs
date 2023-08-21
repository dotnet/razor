// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.AspNetCore.Razor.Language.Components;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Language.IntegrationTests;

public class ComponentUnknownAttributeDiagnosticPassTest : RazorIntegrationTestBase
{
    public ComponentUnknownAttributeDiagnosticPassTest()
    {
        Pass = new ComponentUnknownAttributeDiagnosticPass();
        ProjectEngine = (DefaultRazorProjectEngine)RazorProjectEngine.Create(
            RazorConfiguration.Default,
            RazorProjectFileSystem.Create(Environment.CurrentDirectory),
            b =>
            {
                // Don't run the markup mutating passes.
                b.Features.Remove(b.Features.OfType<ComponentMarkupDiagnosticPass>().Single());
                b.Features.Remove(b.Features.OfType<ComponentMarkupBlockPass>().Single());
                b.Features.Remove(b.Features.OfType<ComponentMarkupEncodingPass>().Single());
            });
        Engine = ProjectEngine.Engine;

        Pass.Engine = Engine;
    }

    private DefaultRazorProjectEngine ProjectEngine { get; }
    private RazorEngine Engine { get; }
    private ComponentUnknownAttributeDiagnosticPass Pass { get; set; }
    internal override string FileKind => FileKinds.Component;
    internal override bool UseTwoPhaseCompilation => true;

    [Fact]
    public void Execute_NoInvalidAttributes()
    {
        // Arrange
        AdditionalSyntaxTrees.Add(Parse(@"
using System;
using Microsoft.AspNetCore.Components;

namespace Test
{
    public class MyComponent : ComponentBase
    {
        [Parameter] public int Value { get; set; }
    }
}
"));
        var result = CompileToCSharp(@"<MyComponent Value=""123"" />");
        var document = result.CodeDocument;
        var documentNode = Lower(document);

        // Act
        Pass.Execute(document, documentNode);

        // Assert
        Assert.Empty(documentNode.GetAllDiagnostics());
    }

    [Fact]
    public void Execute_AttributeBinding()
    {
        // Arrange
        AdditionalSyntaxTrees.Add(Parse(@"
using System;
using Microsoft.AspNetCore.Components;

namespace Test
{
    public class MyComponent : ComponentBase
    {
        [Parameter] public int Value { get; set; }
        [Parameter] public EventCallback<int> ValueChanged { get; set; }
    }
}
"));
        var result = CompileToCSharp(@"
<MyComponent @bind-Value=""@_value"" />
@code {
    private int _value = 0;
}
");
        var document = result.CodeDocument;
        var documentNode = Lower(document);

        // Act
        Pass.Execute(document, documentNode);

        // Assert
        Assert.Empty(documentNode.GetAllDiagnostics());
    }

    [Fact]
    public void Execute_OneInvalidAttribute()
    {
        // Arrange
        AdditionalSyntaxTrees.Add(Parse(@"
using System;
using Microsoft.AspNetCore.Components;

namespace Test
{
    public class MyComponent : ComponentBase
    {
        [Parameter] public int Value { get; set; }
    }
}
"));
        var result = CompileToCSharp(@"<MyComponent InvalidAttribute=""123"" />");
        var document = result.CodeDocument;
        var documentNode = Lower(document);

        // Act
        Pass.Execute(document, documentNode);

        // Assert
        var diagnostic = Assert.Single(documentNode.GetAllDiagnostics());
        Assert.Equal(ComponentDiagnosticFactory.UnknownMarkupAttribute.Id, diagnostic.Id);

        var node = documentNode.FindDescendantNodes<ComponentAttributeIntermediateNode>().Where(n => n.HasDiagnostics).Single();
        Assert.Equal("InvalidAttribute", node.AttributeName);
    }

    [Fact]
    public void Execute_CaptureAdditionalAttributes()
    {
        // Arrange
        AdditionalSyntaxTrees.Add(Parse(@"
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

namespace Test
{
    public class MyComponent : ComponentBase
    {
        [Parameter] public int Value { get; set; }

        [Parameter(CaptureUnmatchedValues = true)]
        public IDictionary<string, object> AdditionalAttributes { get; set; }
    }
}
"));
        var result = CompileToCSharp(@"<MyComponent InvalidAttribute=""123"" />");
        var document = result.CodeDocument;
        var documentNode = Lower(document);

        // Act
        Pass.Execute(document, documentNode);

        // Assert
        Assert.Empty(documentNode.GetAllDiagnostics());
    }

    private DocumentIntermediateNode Lower(RazorCodeDocument codeDocument)
    {
        for (var i = 0; i < Engine.Phases.Count; i++)
        {
            var phase = Engine.Phases[i];
            if (phase is IRazorCSharpLoweringPhase)
            {
                break;
            }

            phase.Execute(codeDocument);
        }

        var document = codeDocument.GetDocumentIntermediateNode();
        Engine.Features.OfType<ComponentDocumentClassifierPass>().Single().Execute(codeDocument, document);
        return document;
    }
}
