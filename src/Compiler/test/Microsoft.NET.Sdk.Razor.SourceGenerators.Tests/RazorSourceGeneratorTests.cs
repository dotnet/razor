// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    public sealed class RazorSourceGeneratorTests : RazorSourceGeneratorTestsBase
    {
        [Fact]
        public async Task SourceGenerator_RazorFiles_Works()
        {
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
            });

            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project);

            var result = RunGenerator(compilation!, ref driver)
                            .VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Single(result.GeneratedSources);
        }

        [Fact, WorkItem("https://github.com/dotnet/razor/issues/8610")]
        public async Task SourceGenerator_RazorFiles_UsingAlias_NestedClass()
        {
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = """
                    @code {
                        public class MyModel { }
                    }
                    """,
                ["Shared/MyComponent.razor"] = """
                    @using MyAlias = Pages.Index.MyModel;

                    <MyComponent Data="@Data" />

                    @code {
                        [Parameter]
                        public MyAlias? Data { get; set; }
                    }
                    """,
            });
            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project, options =>
            {
                options.TestGlobalOptions["build_property.RazorLangVersion"] = "7.0";
            });

            // Act
            var result = RunGenerator(compilation!, ref driver);

            // Assert
            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);
        }

        [Fact]
        public async Task SourceGenerator_RazorFiles_DesignTime()
        {
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
            });

            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts, optionsProvider) = await GetDriverWithAdditionalTextAndProviderAsync(project, hostOutputs: true);

            // Enable design-time.
            var options = optionsProvider.Clone();
            options.TestGlobalOptions["build_property.RazorDesignTime"] = "true";
            options.TestGlobalOptions["build_property.EnableRazorHostOutputs"] = "true";
            var driver2 = driver.WithUpdatedAnalyzerConfigOptions(options);

            var result = RunGenerator(compilation!, ref driver2);
            result.VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Single(result.GeneratedSources);

            // Enable design-time without host outputs.
            options = optionsProvider.Clone();
            options.TestGlobalOptions["build_property.RazorDesignTime"] = "true";
            driver2 = driver.WithUpdatedAnalyzerConfigOptions(options);

            result = RunGenerator(compilation!, ref driver2)
                        .VerifyOutputsMatch(result, 2, expectedHost: false);

            Assert.Empty(result.Diagnostics);
            Assert.Single(result.GeneratedSources);
        }

        [Fact]
        public async Task SourceGeneratorEvents_RazorFiles_Works()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project);

            var result = RunGenerator(compilation!, ref driver)
                            .VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
                e => Assert.Equal("ComputeRazorSourceGeneratorOptions", e.EventName),
                e => e.AssertSingleItem("GenerateDeclarationCodeStart", "/Pages/Index.razor"),
                e => e.AssertSingleItem("GenerateDeclarationCodeStop", "/Pages/Index.razor"),
                e => e.AssertSingleItem("GenerateDeclarationCodeStart", "/Pages/Counter.razor"),
                e => e.AssertSingleItem("GenerateDeclarationCodeStop", "/Pages/Counter.razor"),
                e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
                e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName),
                e => Assert.Equal("DiscoverTagHelpersFromReferencesStart", e.EventName),
                e => Assert.Equal("DiscoverTagHelpersFromReferencesStop", e.EventName),
                e => e.AssertPair("RazorCodeGenerateStart", "/Pages/Index.razor", "Runtime"),
                e => e.AssertPair("RazorCodeGenerateStop", "/Pages/Index.razor", "Runtime"),
                e => e.AssertPair("RazorCodeGenerateStart", "/Pages/Counter.razor", "Runtime"),
                e => e.AssertPair("RazorCodeGenerateStop", "/Pages/Counter.razor", "Runtime"),
                e => e.AssertSingleItem("AddSyntaxTrees", "Pages_Index_razor.g.cs"),
                e => e.AssertSingleItem("AddSyntaxTrees", "Pages_Counter_razor.g.cs")
            );
        }

        [Fact]
        public async Task IncrementalCompilation_DoesNotReexecuteSteps_WhenRazorFilesAreUnchanged()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project);

            var result = RunGenerator(compilation!, ref driver)
                            .VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver)
                        .VerifyOutputsMatch(result);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Empty(eventListener.Events);
        }

        [Fact]
        public async Task IncrementalCompilation_WhenRazorFileMarkupChanges()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver)
                            .VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver)
                        .VerifyOutputsMatch(result);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Empty(eventListener.Events);

            var updatedText = new TestAdditionalText("Pages/Counter.razor", SourceText.From("<h2>Counter</h2>", Encoding.UTF8));
            driver = driver.ReplaceAdditionalText(additionalTexts.First(f => f.Path == updatedText.Path), updatedText);

            result = RunGenerator(compilation!, ref driver)
                        .VerifyOutputsMatch(result, n: 2, diffs: new[] { 1 });

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e => e.AssertSingleItem("GenerateDeclarationCodeStart", "/Pages/Counter.razor"),
               e => e.AssertSingleItem("GenerateDeclarationCodeStop", "/Pages/Counter.razor"),
               e => e.AssertPair("RazorCodeGenerateStart", "/Pages/Counter.razor", "Runtime"),
               e => e.AssertPair("RazorCodeGenerateStop", "/Pages/Counter.razor", "Runtime"),
               e => e.AssertSingleItem("AddSyntaxTrees", "Pages_Counter_razor.g.cs")
            );
        }

        [Fact]
        public async Task IncrementalCompilation_RazorFiles_WhenNewTypeIsAdded()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver)
                            .VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver)
                        .VerifyOutputsMatch(result);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            project = project.AddDocument("Person.cs", SourceText.From(@"
public class Person
{
    public string Name { get; set; }
}", Encoding.UTF8)).Project;
            compilation = await project.GetCompilationAsync();

            result = RunGenerator(compilation!, ref driver,
                // Person.cs(4,19): warning CS8618: Non-nullable property 'Name' must contain a non-null value when exiting constructor. Consider declaring the property as nullable.
                d => Assert.Equal(("SourceFile(Person.cs[44..48))", "CS8618"), (d.Location.ToString(), d.Id)));

            result.VerifyOutputsMatch(result);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName));
        }

        [Fact]
        public async Task IncrementalCompilation_RazorFiles_WhenCSharpTypeChanges()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            },
            new()
            {
                ["Person.cs"] = @"
public class Person
{
    public string Name { get; set; }
}"
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var expectedDiagnostics = new Action<Diagnostic>[]
            {
                // Person.cs(4,19): warning CS8618: Non-nullable property 'Name' must contain a non-null value when exiting constructor. Consider declaring the property as nullable.
                d => Assert.Equal(("SourceFile(Person.cs[44..48))", "CS8618"), (d.Location.ToString(), d.Id))
            };

            var result = RunGenerator(compilation!, ref driver, expectedDiagnostics)
                            .VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver, expectedDiagnostics)
                        .VerifyOutputsMatch(result);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            project = project.Documents.First().WithText(SourceText.From(@"
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}", Encoding.UTF8)).Project;
            compilation = await project.GetCompilationAsync();

            result = RunGenerator(compilation!, ref driver, expectedDiagnostics)
                        .VerifyOutputsMatch(result);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName));
        }

        [Fact]
        public async Task IncrementalCompilation_RazorFiles_WhenChildComponentsAreAdded()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver)
                            .VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver)
                        .VerifyOutputsMatch(result);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Empty(eventListener.Events);

            var updatedText = new TestAdditionalText("Pages/Counter.razor", SourceText.From(@"
<h2>Counter</h2>
<h3>Current count: @count</h3>
<button @onclick=""Click"">Click me</button>

@code
{
    private int count;

    public void Click() => count++;
}

", Encoding.UTF8));
            driver = driver.ReplaceAdditionalText(additionalTexts.First(f => f.Path == updatedText.Path), updatedText);

            result = RunGenerator(compilation!, ref driver)
                        .VerifyOutputsMatch(result, n: 2, diffs: new[] { 1 });

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e => e.AssertSingleItem("GenerateDeclarationCodeStart", "/Pages/Counter.razor"),
               e => e.AssertSingleItem("GenerateDeclarationCodeStop", "/Pages/Counter.razor"),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName),
               e => e.AssertPair("RazorCodeGenerateStart", "/Pages/Counter.razor", "Runtime"),
               e => e.AssertPair("RazorCodeGenerateStop", "/Pages/Counter.razor", "Runtime"),
               e => e.AssertSingleItem("AddSyntaxTrees", "Pages_Counter_razor.g.cs")
            );
        }

        [Fact]
        public async Task IncrementalCompilation_RazorFiles_WhenNewComponentParameterIsAdded()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver)
                            .VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver)
                        .VerifyOutputsMatch(result);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Empty(eventListener.Events);

            var updatedText = new TestAdditionalText("Pages/Counter.razor", SourceText.From(@"
<h2>Counter</h2>
<h3>Current count: @count</h3>
<button @onclick=""Click"">Click me</button>

@code
{
    private int count;

    public void Click() => count++;

    [Parameter] public int IncrementAmount { get; set; }
}

", Encoding.UTF8));
            driver = driver.ReplaceAdditionalText(additionalTexts.First(f => f.Path == updatedText.Path), updatedText);

            result = RunGenerator(compilation!, ref driver)
                        .VerifyOutputsMatch(result, n: 2, diffs: new[] { 1 });

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e => e.AssertSingleItem("GenerateDeclarationCodeStart", "/Pages/Counter.razor"),
               e => e.AssertSingleItem("GenerateDeclarationCodeStop", "/Pages/Counter.razor"),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName),
               e => e.AssertPair("RazorCodeGenerateStart", "/Pages/Index.razor", "Runtime"),
               e => e.AssertPair("RazorCodeGenerateStop", "/Pages/Index.razor", "Runtime"),
               e => e.AssertPair("RazorCodeGenerateStart", "/Pages/Counter.razor", "Runtime"),
               e => e.AssertPair("RazorCodeGenerateStop", "/Pages/Counter.razor", "Runtime"),
               e => e.AssertSingleItem("AddSyntaxTrees", "Pages_Counter_razor.g.cs")
            );
        }

        [Fact]
        public async Task IncrementalCompilation_RazorFiles_WhenProjectReferencesChange()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] =
@"
@using SurveyPromptRootNamspace;
<h1>Hello world</h1>
<SurveyPrompt />
",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver,
                // Pages/Index.razor(2,7): error CS0246: The type or namespace name 'SurveyPromptRootNamspace' could not be found (are you missing a using directive or an assembly reference?)
                d => Assert.Equal(("SourceFile(Microsoft.NET.Sdk.Razor.SourceGenerators\\Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator\\Pages_Index_razor.g.cs[473..497))", "CS0246"), (d.Location.ToString(), d.Id)));

            result.VerifyOutputsMatchBaseline();

            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("RZ10012", diagnostic.Id);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            var surveyPromptAssembly = GetSurveyPromptMetadataReference(compilation!);
            compilation = compilation!.AddReferences(surveyPromptAssembly);

            result = RunGenerator(compilation, ref driver)
                        .VerifyOutputsMatch(result, n: 2, diffs: new[] { 0 });

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromReferencesStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromReferencesStop", e.EventName),
               e => e.AssertPair("RazorCodeGenerateStart", "/Pages/Index.razor", "Runtime"),
               e => e.AssertPair("RazorCodeGenerateStop", "/Pages/Index.razor", "Runtime"),
               e => e.AssertPair("RazorCodeGenerateStart", "/Pages/Counter.razor", "Runtime"),
               e => e.AssertPair("RazorCodeGenerateStop", "/Pages/Counter.razor", "Runtime"),
               e => e.AssertSingleItem("AddSyntaxTrees", "Pages_Index_razor.g.cs")
            );

            // Verify caching
            eventListener.Events.Clear();
            result = RunGenerator(compilation, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);
            Assert.Empty(eventListener.Events);

            static MetadataReference GetSurveyPromptMetadataReference(Compilation currentCompilation)
            {
                var updatedCompilation = currentCompilation.RemoveAllSyntaxTrees()
                    .WithAssemblyName("SurveyPromptAssembly")
                    .AddSyntaxTrees(CSharpSyntaxTree.ParseText(@"
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
namespace SurveyPromptRootNamspace;
public class SurveyPrompt : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder) {}
}"));
                var stream = new MemoryStream();
                var emitResult = updatedCompilation.Emit(stream);
                Assert.True(emitResult.Success);

                stream.Position = 0;
                return MetadataReference.CreateFromStream(stream);
            }
        }

        [Fact]
        public async Task SourceGenerator_CshtmlFiles_Works()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.cshtml"] = "<h1>Hello world</h1>",
                ["Views/Shared/_Layout.cshtml"] = "<h1>Layout</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project);

            var result = RunGenerator(compilation!, ref driver)
                            .VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
                e => Assert.Equal("ComputeRazorSourceGeneratorOptions", e.EventName),
                e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
                e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName),
                e => Assert.Equal("DiscoverTagHelpersFromReferencesStart", e.EventName),
                e => Assert.Equal("DiscoverTagHelpersFromReferencesStop", e.EventName),
                e => e.AssertPair("RazorCodeGenerateStart", "/Pages/Index.cshtml", "Runtime"),
                e => e.AssertPair("RazorCodeGenerateStop", "/Pages/Index.cshtml", "Runtime"),
                e => e.AssertPair("RazorCodeGenerateStart", "/Views/Shared/_Layout.cshtml", "Runtime"),
                e => e.AssertPair("RazorCodeGenerateStop", "/Views/Shared/_Layout.cshtml", "Runtime"),
                e => e.AssertSingleItem("AddSyntaxTrees", "Pages_Index_cshtml.g.cs"),
                e => e.AssertSingleItem("AddSyntaxTrees", "Views_Shared__Layout_cshtml.g.cs")
            );
        }

        [Fact, WorkItem("https://github.com/dotnet/razor/issues/7049")]
        public async Task SourceGenerator_CshtmlFiles_TagHelperInFunction()
        {
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.cshtml"] = """
                @addTagHelper *, TestProject
                @{ await RenderMyRazor(); }
                @functions {
                    async Task RenderMyRazor()
                    {
                        <email>first tag helper</email>
                        <email>
                            second tag helper
                            <email>nested tag helper</email>
                        </email>
                    }
                }
                """,
            }, new()
            {
                ["EmailTagHelper.cs"] = """
                using Microsoft.AspNetCore.Razor.TagHelpers;
                public class EmailTagHelper : TagHelper
                {
                    public override void Process(TagHelperContext context, TagHelperOutput output)
                    {
                        output.TagName = "a";
                    }
                }
                """
            });
            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project);

            // Act
            var result = RunGenerator(compilation!, ref driver,
                // warning CS1998: This async method lacks 'await' operators and will run synchronously. Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread.
                d => Assert.StartsWith("Microsoft.NET.Sdk.Razor.SourceGenerators\\Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator\\Pages_Index_cshtml.g.cs(64,207): warning CS1998: ", d.ToString()),
                d => Assert.StartsWith("Microsoft.NET.Sdk.Razor.SourceGenerators\\Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator\\Pages_Index_cshtml.g.cs(80,211): warning CS1998: ", d.ToString()));

            // Assert
            Assert.Empty(result.Diagnostics);
            Assert.Single(result.GeneratedSources);
        }

        [Fact, WorkItem("https://github.com/dotnet/razor/issues/7049")]
        public async Task SourceGenerator_CshtmlFiles_TagHelperInFunction_ManualSuppression()
        {
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.cshtml"] = """
                @addTagHelper *, TestProject

                @{ await RenderMyRazor(); }

                @functions {
                    #pragma warning disable 1998
                    async Task RenderMyRazor()
                    {
                        var lambdaWithUnnecessaryAsync1 = async () => { };
                        <email>first tag helper</email>
                        <email>
                            second tag helper
                            <email>nested tag helper</email>
                        </email>
                        var lambdaWithUnnecessaryAsync2 = async () => { };
                    }
                }
                """,
            }, new()
            {
                ["EmailTagHelper.cs"] = """
                using Microsoft.AspNetCore.Razor.TagHelpers;

                public class EmailTagHelper : TagHelper
                {
                    public override void Process(TagHelperContext context, TagHelperOutput output)
                    {
                        output.TagName = "a";
                    }
                }
                """
            });
            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project);

            // Act
            var result = RunGenerator(compilation!, ref driver);

            // Assert
            Assert.Empty(result.Diagnostics);
            Assert.Single(result.GeneratedSources);
        }

        [Fact, WorkItem("https://github.com/dotnet/razor/issues/7049")]
        public async Task SourceGenerator_CshtmlFiles_TagHelperInBody()
        {
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.cshtml"] = """
                @addTagHelper *, TestProject

                <email>tag helper</email>
                @section MySection {
                    <p>my section</p>
                }
                """,
            }, new()
            {
                ["EmailTagHelper.cs"] = """
                using Microsoft.AspNetCore.Razor.TagHelpers;

                public class EmailTagHelper : TagHelper
                {
                    public override void Process(TagHelperContext context, TagHelperOutput output)
                    {
                        output.TagName = "a";
                    }
                }
                """
            });
            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project);

            // Act
            var result = RunGenerator(compilation!, ref driver);

            // Assert
            Assert.Empty(result.Diagnostics);
            Assert.Single(result.GeneratedSources);
        }

        [Fact]
        public async Task SourceGenerator_CshtmlFiles_WhenMarkupChanges()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.cshtml"] = "<h1>Hello world</h1>",
                ["Views/Shared/_Layout.cshtml"] = "<h1>Layout</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver)
                            .VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver)
                        .VerifyOutputsMatch(result);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Empty(eventListener.Events);

            var updatedText = new TestAdditionalText("Views/Shared/_Layout.cshtml", SourceText.From("<h2>Updated Layout</h2>", Encoding.UTF8));
            driver = driver.ReplaceAdditionalText(additionalTexts.First(f => f.Path == updatedText.Path), updatedText);

            result = RunGenerator(compilation!, ref driver)
                        .VerifyOutputsMatch(result, n: 2, diffs: new[] { 1 });

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e => e.AssertPair("RazorCodeGenerateStart", "/Views/Shared/_Layout.cshtml", "Runtime"),
               e => e.AssertPair("RazorCodeGenerateStop", "/Views/Shared/_Layout.cshtml", "Runtime"),
               e => e.AssertSingleItem("AddSyntaxTrees", "Views_Shared__Layout_cshtml.g.cs")
            );
        }

        [Fact]
        public async Task SourceGenerator_CshtmlFiles_CSharpTypeChanges()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.cshtml"] = "<h1>Hello world</h1>",
                ["Views/Shared/_Layout.cshtml"] = "<h1>Layout</h1>",
            },
            new()
            {
                ["Person.cs"] = @"
public class Person
{
    public string Name { get; set; }
}"
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var expectedDiagnostics = new Action<Diagnostic>[]
            {
                // Person.cs(4,19): warning CS8618: Non-nullable property 'Name' must contain a non-null value when exiting constructor. Consider declaring the property as nullable.
                d => Assert.Equal(("SourceFile(Person.cs[44..48))", "CS8618"), (d.Location.ToString(), d.Id))
            };

            var result = RunGenerator(compilation!, ref driver, expectedDiagnostics)
                            .VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver, expectedDiagnostics)
                       .VerifyOutputsMatch(result);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            project = project.Documents.First().WithText(SourceText.From(@"
public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}", Encoding.UTF8)).Project;
            compilation = await project.GetCompilationAsync();

            result = RunGenerator(compilation!, ref driver, expectedDiagnostics)
                        .VerifyOutputsMatch(result);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName));

        }

        [Fact]
        public async Task SourceGenerator_CshtmlFiles_NewTagHelper()
        {
            // Arrange
            using var eventListener = new RazorEventListener();
            var project = CreateTestProject(new()
            {
                ["Pages/Index.cshtml"] =
@"
@addTagHelper *, TestProject
<h2>Hello world</h2>",
                ["Views/Shared/_Layout.cshtml"] = "<h1>Layout</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver)
                            .VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            eventListener.Events.Clear();

            result = RunGenerator(compilation!, ref driver)
                        .VerifyOutputsMatch(result);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            project = project.AddDocument("HeaderTagHelper.cs", SourceText.From(@"
using Microsoft.AspNetCore.Razor.TagHelpers;
namespace MyApp;

[HtmlTargetElement(""h2"")]
public class HeaderTagHelper : TagHelper
{
    public override int Order => 0;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.Add(""role"", ""heading"");
    }
}", Encoding.UTF8)).Project;
            compilation = await project.GetCompilationAsync();

            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            Assert.Collection(eventListener.Events,
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStart", e.EventName),
               e => Assert.Equal("DiscoverTagHelpersFromCompilationStop", e.EventName),
               e => e.AssertPair("RazorCodeGenerateStart", "/Pages/Index.cshtml", "Runtime"),
               e => e.AssertPair("RazorCodeGenerateStop", "/Pages/Index.cshtml", "Runtime"),
               e => e.AssertPair("RazorCodeGenerateStart", "/Views/Shared/_Layout.cshtml", "Runtime"),
               e => e.AssertPair("RazorCodeGenerateStop", "/Views/Shared/_Layout.cshtml", "Runtime"),
               e => e.AssertSingleItem("AddSyntaxTrees", "Pages_Index_cshtml.g.cs")
           );
        }

        [Fact]
        public async Task SourceGenerator_CshtmlFiles_RazorDiagnostics_Fixed()
        {
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.cshtml"] =
@"
@{
<h1>Malformed h1
}
</h1>",
                ["Views/Shared/_Layout.cshtml"] = "<h1>Layout</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver)
                            .VerifyOutputsMatchBaseline();

            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("RZ1006", diagnostic.Id);
            Assert.Equal(2, result.GeneratedSources.Length);

            var updatedText = new TestAdditionalText("Pages/Index.cshtml", SourceText.From("<h1>Fixed header</h1>", Encoding.UTF8));
            driver = driver.ReplaceAdditionalText(additionalTexts.First(f => f.Path == updatedText.Path), updatedText);

            result = RunGenerator(compilation!, ref driver)
                        .VerifyOutputsMatch(result, n: 2, diffs: new[] { 0 });

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);
        }

        [Fact]
        public async Task SourceGenerator_CshtmlFiles_RazorDiagnostics_Introduced()
        {
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.cshtml"] = "<h1>Valid h1</h1>",
                ["Views/Shared/_Layout.cshtml"] = "<h1>Layout</h1>",
            });
            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project);

            var result = RunGenerator(compilation!, ref driver)
                            .VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Equal(2, result.GeneratedSources.Length);

            var updatedText = new TestAdditionalText("Pages/Index.cshtml", SourceText.From(@"
@{
<h1>Malformed h1
}
</h1>", Encoding.UTF8));
            driver = driver.ReplaceAdditionalText(additionalTexts.First(f => f.Path == updatedText.Path), updatedText);

            result = RunGenerator(compilation!, ref driver)
                        .VerifyOutputsMatch(result, n: 2, diffs: new[] { 0 });

            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal("RZ1006", diagnostic.Id);
            Assert.Equal(2, result.GeneratedSources.Length);
        }

        [Fact, WorkItem("https://github.com/dotnet/razor/issues/8281")]
        public async Task SourceGenerator_CshtmlFiles_ViewComponentTagHelper()
        {
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Views/Home/Index.cshtml"] = """
                @addTagHelper *, TestProject
                <vc:test first-name="Jan" />
                """,
            }, new()
            {
                ["TestViewComponent.cs"] = """
                public class TestViewComponent
                {
                    public string Invoke(string firstName)
                    {
                        return firstName;
                    }
                }
                """,
            });
            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project);

            // Act
            var result = RunGenerator(compilation!, ref driver);

            // Assert
            result.VerifyOutputsMatchBaseline();
            Assert.Empty(result.Diagnostics);
            Assert.Single(result.GeneratedSources);
        }

        [Fact, WorkItem("https://github.com/dotnet/aspnetcore/issues/36227")]
        public async Task SourceGenerator_DoesNotUpdateSources_WhenSourceGeneratorIsSuppressed()
        {
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });

            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts, optionsProvider) = await GetDriverWithAdditionalTextAndProviderAsync(project);

            var result = RunGenerator(compilation!, ref driver);
            result.VerifyOutputsMatchBaseline();

            // now run the generator with suppression
            var suppressedOptions = optionsProvider.Clone();
            suppressedOptions.TestGlobalOptions["build_property.SuppressRazorSourceGenerator"] = "true";
            driver = driver.WithUpdatedAnalyzerConfigOptions(suppressedOptions);

            // results should be empty
            var emptyResult = RunGenerator(compilation!, ref driver);
            Assert.Empty(emptyResult.GeneratedSources);

            // now unsuppress and re-run
            driver = driver.WithUpdatedAnalyzerConfigOptions(optionsProvider);

            result = RunGenerator(compilation!, ref driver)
                .VerifyOutputsMatch(result);
        }

        [Fact, WorkItem("https://github.com/dotnet/razor/issues/7914")]
        public async Task SourceGenerator_UppercaseRazor_GeneratesComponent()
        {
            var project = CreateTestProject(new()
            {
                ["Component.Razor"] = "<h1>Hello world</h1>",
            });

            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project);

            var result = RunGenerator(compilation!, ref driver)
                            .VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Single(result.GeneratedSources);
        }

        [Theory, WorkItem("https://github.com/dotnet/razor/issues/7236")]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("\n")]
        public async Task SourceGenerator_EmptyTargetPath(string targetPath)
        {
            const string componentPath = "Component.razor";
            var project = CreateTestProject(new()
            {
                [componentPath] = "<h1>Hello world</h1>",
            });

            var compilation = await project.GetCompilationAsync();
            var (driver, _, _) = await GetDriverWithAdditionalTextAndProviderAsync(project, optionsProvider =>
            {
                optionsProvider.AdditionalTextOptions[componentPath] = new TestAnalyzerConfigOptions
                {
                    ["build_metadata.AdditionalFiles.TargetPath"] = targetPath
                };
            });

            var result = RunGenerator(compilation!, ref driver);

            var diagnostic = Assert.Single(result.Diagnostics);
            // RSG002: TargetPath not specified for additional file
            Assert.Equal("RSG002", diagnostic.Id);

            Assert.Empty(result.GeneratedSources);
        }

        [Fact]
        public async Task SourceGenerator_Class_Inside_CodeBlock()
        {
            var project = CreateTestProject(new()
            {
                ["Component.Razor"] =
"""
<h1>Hello world</h1>

@code
{
    public class X {}
}
"""});

            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project);

            var result = RunGenerator(compilation!, ref driver)
                            .VerifyOutputsMatchBaseline();

            Assert.Empty(result.Diagnostics);
            Assert.Single(result.GeneratedSources);
        }
    }
}
