// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using static Roslyn.Test.Utilities.TestHelpers;

namespace Microsoft.AspNetCore.Razor.Language.IntegrationTests;

public class ComponentRenderModeDirectiveIntegrationTests : RazorIntegrationTestBase
{
    public ComponentRenderModeDirectiveIntegrationTests()
    {
        // Include the required runtime source
        BaseCompilation = DefaultBaseCompilation.AddSyntaxTrees(Parse(RenderModeAttribute));
    }

    internal override CSharpCompilation BaseCompilation { get; }

    internal override string FileKind => FileKinds.Component;

    [Fact]
    public void RenderMOde_With_Fully_Qualified_Type()
    {
        // Arrange & Act
        var component = CompileToComponent($@"
@rendermode Microsoft.AspNetCore.Components.DefaultRenderModes.Server
");

        // Assert
        var attribute = Assert.Single(component.GetType().CustomAttributes);
        Assert.Equal("PrivateComponentRenderModeAttribute", attribute.AttributeType.Name);

        var nestedType = component.GetType().GetNestedType("PrivateComponentRenderModeAttribute", System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(nestedType);

        var modeProperty = nestedType.GetProperty("Mode");
        Assert.NotNull(modeProperty);
    }

    [Fact]
    public void RenderMode_With_Static_Usings()
    {
        // Arrange & Act
        var component = CompileToComponent($@"
@using static Microsoft.AspNetCore.Components.DefaultRenderModes
@rendermode Server
");

        // Assert
        var attribute = Assert.Single(component.GetType().CustomAttributes);
        Assert.Equal("PrivateComponentRenderModeAttribute", attribute.AttributeType.Name);

        var nestedType = component.GetType().GetNestedType("PrivateComponentRenderModeAttribute", System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(nestedType);

        var modeProperty = nestedType.GetProperty("Mode");
        Assert.NotNull(modeProperty);
    }

    [Fact]
    public void RenderMode_Missing_Value()
    {
        // Arrange & Act
        var compilationResult = CompileToCSharp($@"
@rendermode
");

        // Assert
        // Error RZ1014: The 'rendermode' directive expects a namespace name.
        var diagnostic = Assert.Single(compilationResult.Diagnostics);
        Assert.Equal("RZ1014", diagnostic.Id);
    }

    [Fact]
    public void DuplicateRenderModes()
    {
        // Arrange & Act
        var compilationResult = CompileToCSharp($@"
@rendermode Microsoft.AspNetCore.Components.DefaultRenderModes.Server
@rendermode Microsoft.AspNetCore.Components.DefaultRenderModes.Server

");

        // Assert
        //Error RZ2001: The 'rendermode' directive may only occur once per document.
        var diagnostic = Assert.Single(compilationResult.Diagnostics);
        Assert.Equal("RZ2001", diagnostic.Id);
    }

    [Fact]
    public void RenderMode_With_InvalidIdentifier()
    {
        var compilationResult = CompileToCSharp($@"
@rendermode NoExist
");

        Assert.Empty(compilationResult.Diagnostics);

        var assemblyResult = CompileToAssembly(compilationResult, throwOnFailure: false);
        assemblyResult.Diagnostics.Verify(
                    // x:\dir\subdir\Test\TestComponent.cshtml(1,58): error CS0103: The name 'NoExist' does not exist in the current context
                    //             public override IComponentRenderMode Mode => NoExist;
                    Diagnostic(ErrorCode.ERR_NameNotInContext, "NoExist").WithArguments("NoExist").WithLocation(1, 58));
    }

    [Fact]
    public void LanguageVersion()
    {
        var compilationResult = CompileToCSharp($@"
@rendermode Microsoft.AspNetCore.Components.DefaultRenderModes.Server
", configuration: Configuration.WithVersion(RazorLanguageVersion.Version_7_0));

        Assert.Empty(compilationResult.Diagnostics);

        var assemblyResult = CompileToAssembly(compilationResult, throwOnFailure: false);
        assemblyResult.Diagnostics.Verify(
            // x:\dir\subdir\Test\TestComponent.cshtml(1,3): error CS0103: The name 'rendermode' does not exist in the current context
            // __builder.AddContent(0, rendermode);
            Diagnostic(ErrorCode.ERR_NameNotInContext, "rendermode").WithArguments("rendermode").WithLocation(1, 3)
            );
    }

    private const string RenderModeAttribute = """
 using System;
 namespace Microsoft.AspNetCore.Components;

 public interface IComponentRenderMode
 {
 }

 [AttributeUsage(AttributeTargets.Class)]
 public abstract class RenderModeAttribute : Attribute
 {
     public abstract IComponentRenderMode Mode { get; }
 }

 public class DefaultRenderModes : IComponentRenderMode
 {
    public static IComponentRenderMode Server = null!;
 }
 """;
}

