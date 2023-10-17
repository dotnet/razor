﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Test.Utilities;
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
            ["Views/Home/Index.cshtml"] = """
                @(await Html.RenderComponentAsync<MyApp.Shared.Component1>(RenderMode.Static))
                """,
            ["Shared/Component1.razor"] = """
                <Component2 Param="42" />
                """,
            ["Shared/Component2.razor"] = """
                @inherits ComponentBase

                Value: @(Param + 1)

                @code {
                    [Parameter]
                    public int Param { get; set; }
                }
                """
        }, new()
        {
            ["Component2.razor.cs"] = """
                using Microsoft.AspNetCore.Components;

                namespace MyApp.Shared;

                public partial class Component2 : ComponentBase { }
                """
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project, options =>
        {
            options.TestGlobalOptions["build_property.RazorLangVersion"] = "7.0";
        });

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        Assert.Empty(result.Diagnostics);
        Assert.Equal(3, result.GeneratedSources.Length);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8718")]
    public async Task PartialClass_NoBaseInCSharp()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                @(await Html.RenderComponentAsync<MyApp.Shared.Component1>(RenderMode.Static))
                """,
            ["Shared/Component1.razor"] = """
                <Component2 Param="42" />
                """,
            ["Shared/Component2.razor"] = """
                @inherits ComponentBase

                Value: @(Param + 1)

                @code {
                    [Parameter]
                    public int Param { get; set; }
                }
                """
        }, new()
        {
            ["Component2.razor.cs"] = """
                using Microsoft.AspNetCore.Components;

                namespace MyApp.Shared;

                public partial class Component2 { }
                """
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project, options =>
        {
            options.TestGlobalOptions["build_property.RazorLangVersion"] = "7.0";
        });

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        Assert.Empty(result.Diagnostics);
        Assert.Equal(3, result.GeneratedSources.Length);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8718")]
    public async Task ComponentInheritsFromComponent()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                @(await Html.RenderComponentAsync<MyApp.Shared.Component1>(RenderMode.Static))
                """,
            ["Shared/Component1.razor"] = """
                Hello from Component1
                <DerivedComponent />
                """,
            ["Shared/BaseComponent.razor"] = """
                Hello from Base
                """,
            ["Shared/DerivedComponent.razor"] = """
                @inherits BaseComponent
                Hello from Derived
                """
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project, options =>
        {
            options.TestGlobalOptions["build_property.RazorLangVersion"] = "7.0";
        });

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        Assert.Empty(result.Diagnostics);
        Assert.Equal(4, result.GeneratedSources.Length);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Theory, CombinatorialData]
    public async Task AddComponentParameter(
        [CombinatorialValues("7.0", "8.0", "Latest")] string langVersion)
    {
        // Arrange.
        var project = CreateTestProject(new()
        {
            ["Shared/Component1.razor"] = """
                <Component1 Param="42" />

                @code {
                    [Parameter]
                    public int Param { get; set; }
                }
                """,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project, options =>
        {
            options.TestGlobalOptions["build_property.RazorLangVersion"] = langVersion;
        });

        // Act.
        var result = RunGenerator(compilation!, ref driver);

        // Assert.
        Assert.Empty(result.Diagnostics);
        var source = Assert.Single(result.GeneratedSources);
        if (langVersion == "7.0")
        {
            // In Razor v7, AddComponentParameter shouldn't be used even if available.
            Assert.Contains("AddAttribute", source.SourceText.ToString());
            Assert.DoesNotContain("AddComponentParameter", source.SourceText.ToString());
        }
        else
        {
            Assert.DoesNotContain("AddAttribute", source.SourceText.ToString());
            Assert.Contains("AddComponentParameter", source.SourceText.ToString());
        }
    }

    [Theory, CombinatorialData]
    public async Task AddComponentParameter_InSource(
        [CombinatorialValues("7.0", "8.0", "Latest")] string langVersion)
    {
        // Arrange.
        var project = CreateTestProject(new()
        {
            ["Shared/Component1.razor"] = """
                <Component1 Param="42" />

                @code {
                    [Parameter]
                    public int Param { get; set; }
                }
                """,
        }, new()
        {
            ["Shims.cs"] = """
                namespace Microsoft.AspNetCore.Components.Rendering
                {
                    public sealed class RenderTreeBuilder
                    {
                        public void OpenComponent<TComponent>(int sequence) { }
                        public void AddAttribute(int sequence, string name, object value) { }
                        public void AddComponentParameter(int sequence, string name, object value) { }
                        public void CloseComponent() { }
                    }
                }
                namespace Microsoft.AspNetCore.Components
                {
                    public sealed class ParameterAttribute : System.Attribute { }
                    public interface IComponent { }
                    public abstract class ComponentBase : IComponent
                    {
                        protected virtual void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) { }
                    }
                }
                namespace Microsoft.AspNetCore.Components.CompilerServices
                {
                    public static class RuntimeHelpers
                    {
                        public static T TypeCheck<T>(T value) => throw null!;
                    }
                }
                """,
        });
        var compilation = await project.GetCompilationAsync();

        // Remove the AspNetCore DLL v7 to avoid clashes.
        var aspnetDll = compilation.References.Single(r => r.Display.EndsWith("Microsoft.AspNetCore.Components.dll", StringComparison.Ordinal));
        compilation = compilation.RemoveReferences(aspnetDll);

        var driver = await GetDriverAsync(project, options =>
        {
            options.TestGlobalOptions["build_property.RazorLangVersion"] = langVersion;
        });

        // Act.
        var result = RunGenerator(compilation!, ref driver);

        // Assert. Behaves as if `AddComponentParameter` wasn't available because
        // the source generator only searches for it in references, not the current compilation.
        Assert.Empty(result.Diagnostics);
        var source = Assert.Single(result.GeneratedSources);
        Assert.Contains("AddAttribute", source.SourceText.ToString());
        Assert.DoesNotContain("AddComponentParameter", source.SourceText.ToString());
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8660")]
    public async Task TypeArgumentsCannotBeInferred()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Shared/Component1.razor"] = """
                @typeparam T

                <Component1 />

                @code {
                    private void M1<T1>() { }
                    private void M2()
                    {
                        M1();
                    }
                }
                """,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver,
            // Shared/Component1.razor(9,9): error CS0411: The type arguments for method 'Component1<T>.M1<T1>()' cannot be inferred from the usage. Try specifying the type arguments explicitly.
            //         M1();
            Diagnostic(ErrorCode.ERR_CantInferMethTypeArgs, "M1").WithArguments("MyApp.Shared.Component1<T>.M1<T1>()").WithLocation(9, 9));

        // Assert
        result.Diagnostics.Verify(
            // Shared/Component1.razor(3,1): error RZ10001: The type of component 'Component1' cannot be inferred based on the values provided. Consider specifying the type arguments directly using the following attributes: 'T'.
            // <Component1 />
            Diagnostic("RZ10001").WithLocation(3, 1));
        Assert.Single(result.GeneratedSources);
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8545")]
    public async Task Doctype_Newline()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                @(await Html.RenderComponentAsync<MyApp.Shared.Component1>(RenderMode.Static))
                """,
            ["Shared/Component1.razor"] = """
                <!DOCTYPE html>
                <html>
                <head><title>Test</title></head>
                <body>
                This is a test
                </body>
                </html>
                """,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        result.Diagnostics.Verify();
        Assert.Equal(2, result.GeneratedSources.Length);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8545")]
    public async Task Doctype_Newline_View()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                <!DOCTYPE html>
                <html>
                <head><title>Test</title></head>
                <body>
                This is a test
                </body>
                </html>
                """,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        result.Diagnostics.Verify();
        Assert.Single(result.GeneratedSources);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8545")]
    public async Task Doctype_NoNewline()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                @(await Html.RenderComponentAsync<MyApp.Shared.Component1>(RenderMode.Static))
                """,
            ["Shared/Component1.razor"] = """
                <!DOCTYPE html> <html>
                <head><title>Test</title></head>
                <body>
                This is a test
                </body>
                </html>
                """,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        result.Diagnostics.Verify();
        Assert.Equal(2, result.GeneratedSources.Length);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8545")]
    public async Task Doctype_NoNewline_View()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                <!DOCTYPE html> <html>
                <head><title>Test</title></head>
                <body>
                This is a test
                </body>
                </html>
                """,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        result.Diagnostics.Verify();
        Assert.Single(result.GeneratedSources);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8545")]
    public async Task Doctype_HtmlComment()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                @(await Html.RenderComponentAsync<MyApp.Shared.Component1>(RenderMode.Static))
                """,
            ["Shared/Component1.razor"] = """
                <!DOCTYPE html> <!-- comment --> <html>
                </html>
                """,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        result.Diagnostics.Verify();
        Assert.Equal(2, result.GeneratedSources.Length);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8545")]
    public async Task Doctype_HtmlComment_View()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                <!DOCTYPE html> <!-- comment --> <html>
                </html>
                """,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        result.Diagnostics.Verify();
        Assert.Single(result.GeneratedSources);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8545")]
    public async Task Doctype_RazorComment()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                @(await Html.RenderComponentAsync<MyApp.Shared.Component1>(RenderMode.Static))
                """,
            ["Shared/Component1.razor"] = """
                <!DOCTYPE html> @* comment *@ <html>
                </html>
                """,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        result.Diagnostics.Verify();
        Assert.Equal(2, result.GeneratedSources.Length);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8545")]
    public async Task Doctype_RazorComment_View()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                <!DOCTYPE html> @* comment *@ <html>
                </html>
                """,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        result.Diagnostics.Verify();
        Assert.Single(result.GeneratedSources);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8545")]
    public async Task Doctype_CSharp()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                @(await Html.RenderComponentAsync<MyApp.Shared.Component1>(RenderMode.Static))
                """,
            ["Shared/Component1.razor"] = """
                <!DOCTYPE html> @("from" + " csharp") and HTML <html>
                </html>
                """,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        result.Diagnostics.Verify();
        Assert.Equal(2, result.GeneratedSources.Length);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/8545")]
    public async Task Doctype_CSharp_View()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                <!DOCTYPE html> @("from" + " csharp") and HTML <html>
                </html>
                """,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        result.Diagnostics.Verify();
        Assert.Single(result.GeneratedSources);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/9204")]
    public async Task ScriptTag()
    {
        // Arrange
        var project = CreateTestProject(new()
        {
            ["Views/Home/Index.cshtml"] = """
                1: <script>alert('hello')</script>
                2: @{ var c = "alert('hello')"; }<script>@c</script>
                3: <div>alert('hello')</div>
                4: <script>
                alert('hello')</script>
                @(await Html.RenderComponentAsync<MyApp.Shared.Component1>(RenderMode.Static))
                """,
            ["Shared/Component1.razor"] = """
                Component:
                1: <script>alert('hello')</script>
                2: @{ var c = "alert('hello')"; }<script>@c</script>
                3: <div>alert('hello')</div>
                4: <script>
                alert('hello')</script>
                """,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver, out compilation);

        // Assert
        result.Diagnostics.Verify();
        Assert.Equal(2, result.GeneratedSources.Length);
        await VerifyRazorPageMatchesBaselineAsync(compilation, "Views_Home_Index");
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/9051")]
    public async Task LineMapping()
    {
        // Arrange
        var source = """
            <p>The solution to all problems is: @(RaiseHere())</p>
            @code
            {
                private int magicNumber = RaiseHere();
                private static int RaiseHere()
                {
                    return 42;
                }
            }
            """;
        var project = CreateTestProject(new()
        {
            ["Shared/Component1.razor"] = source,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver);

        // Assert
        result.Diagnostics.Verify();
        result.VerifyOutputsMatchBaseline();

        var original = project.AdditionalDocuments.Single();
        var originalText = await original.GetTextAsync();
        Assert.Equal(source, originalText.ToString());
        var generated = result.GeneratedSources.Single();
        var generatedText = generated.SourceText;
        var generatedTextString = generatedText.ToString();
        var snippet = "RaiseHere()";

        // Find the snippet three times (at line 0, 3, and 4).
        var expectedLines = new[] { 0, 3, 4 };
        var originalIndex = -1;
        var generatedIndex = -1;
        for (var count = 0; ; count++)
        {
            originalIndex = source.IndexOf(snippet, originalIndex + 1, StringComparison.Ordinal);
            generatedIndex = generatedTextString.IndexOf(snippet, generatedIndex + 1, StringComparison.Ordinal);

            if (count == 3)
            {
                Assert.True(originalIndex < 0);
                Assert.True(generatedIndex < 0);
                break;
            }

            var generatedSpan = new TextSpan(generatedIndex, snippet.Length);
            Assert.Equal(snippet, generatedText.ToString(generatedSpan));
            var mapped = generated.SyntaxTree.GetMappedLineSpan(generatedSpan);
            Assert.True(mapped.IsValid);
            Assert.True(mapped.HasMappedPath);
            Assert.Equal("Shared/Component1.razor", mapped.Path);
            var expectedLine = expectedLines[count];
            Assert.Equal(expectedLine, mapped.StartLinePosition.Line);
            Assert.Equal(expectedLine, mapped.EndLinePosition.Line);
            var mappedSpan = originalText.Lines.GetTextSpan(mapped.Span);
            // https://github.com/dotnet/razor/issues/9051
            // Assert.Equal(snippet, originalText.ToString(mappedSpan));
            // Assert.Equal(new TextSpan(originalIndex, snippet.Length), mappedSpan);
        }
    }

    [Fact, WorkItem("https://github.com/dotnet/razor/issues/9050")]
    public async Task LineMapping_Tabs()
    {
        // Arrange
        var tab = '\t';
        var source = $$"""
            <div>
            {{tab}}@if (true)
            {{tab}}{
            {{tab}}{{tab}}@("code")
            {{tab}}}
            </div>
            """;
        var project = CreateTestProject(new()
        {
            ["Shared/Component1.razor"] = source,
        });
        var compilation = await project.GetCompilationAsync();
        var driver = await GetDriverAsync(project);

        // Act
        var result = RunGenerator(compilation!, ref driver);

        // Assert
        result.Diagnostics.Verify();
        result.VerifyOutputsMatchBaseline();

        var original = project.AdditionalDocuments.Single();
        var originalText = await original.GetTextAsync();
        Assert.Equal(source, originalText.ToString());
        var generated = result.GeneratedSources.Single();
        var generatedText = generated.SourceText;
        var generatedTextString = generatedText.ToString();

        // Find snippets and verify their mapping.
        var snippets = new[] { "true", "code" };
        var expectedLines = new[] { 1, 3 };
        var originalIndex = -1;
        var generatedIndex = -1;
        foreach (var (snippet, expectedLine) in snippets.Zip(expectedLines))
        {
            originalIndex = source.IndexOf(snippet, originalIndex + 1, StringComparison.Ordinal);
            generatedIndex = generatedTextString.IndexOf(snippet, generatedIndex + 1, StringComparison.Ordinal);
            var generatedSpan = new TextSpan(generatedIndex, snippet.Length);
            Assert.Equal(snippet, generatedText.ToString(generatedSpan));
            var mapped = generated.SyntaxTree.GetMappedLineSpan(generatedSpan);
            Assert.True(mapped.IsValid);
            Assert.True(mapped.HasMappedPath);
            Assert.Equal("Shared/Component1.razor", mapped.Path);
            Assert.Equal(expectedLine, mapped.StartLinePosition.Line);
            Assert.Equal(expectedLine, mapped.EndLinePosition.Line);
            var mappedSpan = originalText.Lines.GetTextSpan(mapped.Span);
            // https://github.com/dotnet/razor/issues/9051
            // Assert.Equal(snippet, originalText.ToString(mappedSpan));
            // Assert.Equal(new TextSpan(originalIndex, snippet.Length), mappedSpan);
        }
    }
}
