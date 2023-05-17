// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Xunit;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators;

public sealed class RazorSourceGeneratorComponentTests : RazorSourceGeneratorTestsBase
{
    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8718")]
    public async Task PartialClass()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Shared/Component1.razor"] = """
                <Component2 />
                """,
            ["Shared/Component2.razor"] = """
                @inherits ComponentBase

                @code {
                    [Parameter]
                    public RenderFragment? ChildContent { get; set; }
                }
                """
        }, new()
        {
            ["Component2.razor.cs"] = """
                using Microsoft.AspNetCore.Components;

                namespace MyApp.Shared;

                public partial class Component2 : ComponentBase
                {

                }
                """
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver);

        // Assert
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.GeneratedSources.Length);
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8718")]
    public async Task PartialClass_NoBaseInCSharp()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Shared/Component1.razor"] = """
                <Component2 />
                """,
            ["Shared/Component2.razor"] = """
                @inherits ComponentBase

                @code {
                    [Parameter]
                    public RenderFragment? ChildContent { get; set; }
                }
                """
        }, new()
        {
            ["Component2.razor.cs"] = """
                using Microsoft.AspNetCore.Components;

                namespace MyApp.Shared;

                public partial class Component2
                {

                }
                """
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver);

        // Assert
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.GeneratedSources.Length);
    }
}
