// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
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
        BaseCompilation = (CSharpCompilation)AddRequiredAttributes(DefaultBaseCompilation);
    }

    internal override CSharpCompilation BaseCompilation { get; } 

    internal override string FileKind => FileKinds.Component;

    [Fact]
    public void RenderMode_With_Fully_Qualified_Type()
    {
        // Arrange & Act
        var component = CompileToComponent("""
            @rendermode Microsoft.AspNetCore.Components.DefaultRenderModes.Server
            """);

        // Assert
        var attribute = Assert.Single(component.GetType().CustomAttributes);
        Assert.EndsWith("PrivateComponentRenderModeAttribute", attribute.AttributeType.Name);

        var attributeType = component.GetType().Assembly.GetTypes().Single(t => t.Name.EndsWith("PrivateComponentRenderModeAttribute", StringComparison.Ordinal));
        Assert.NotNull(attributeType);

        var modeProperty = attributeType.GetProperty("Mode");
        Assert.NotNull(modeProperty);

        var instance = Activator.CreateInstance(attributeType);
        Assert.NotNull(instance);

        var modeValue = modeProperty.GetValue(instance);
        Assert.NotNull(modeValue);

        var valueType = modeValue.GetType();
        Assert.Equal("Microsoft.AspNetCore.Components.DefaultRenderModes", valueType.FullName);
    }

    [Fact]
    public void RenderMode_With_Static_Usings()
    {
        // Arrange & Act
        var component = CompileToComponent("""
            @using static Microsoft.AspNetCore.Components.DefaultRenderModes
            @rendermode Server
            """);

        // Assert
        var attribute = Assert.Single(component.GetType().CustomAttributes);
        Assert.EndsWith("PrivateComponentRenderModeAttribute", attribute.AttributeType.Name);

        var attributeType = component.GetType().Assembly.GetTypes().Single(t => t.Name.EndsWith("PrivateComponentRenderModeAttribute", StringComparison.Ordinal));
        Assert.NotNull(attributeType);

        var modeProperty = attributeType.GetProperty("Mode");
        Assert.NotNull(modeProperty);

        var instance = Activator.CreateInstance(attributeType);
        Assert.NotNull(instance);

        var modeValue = modeProperty.GetValue(instance);
        Assert.NotNull(modeValue);

        var valueType = modeValue.GetType();
        Assert.Equal("Microsoft.AspNetCore.Components.DefaultRenderModes", valueType.FullName);
    }

    [Fact]
    public void RenderMode_Missing_Value()
    {
        // Arrange & Act
        var compilationResult = CompileToCSharp("""
            @rendermode

            """);

        // Assert
        // Error RZ1014: The 'rendermode' directive expects a namespace name.
        var diagnostic = Assert.Single(compilationResult.Diagnostics);
        Assert.Equal("RZ1014", diagnostic.Id);
    }

    [Fact]
    public void DuplicateRenderModes()
    {
        // Arrange & Act
        var compilationResult = CompileToCSharp("""
            @rendermode Microsoft.AspNetCore.Components.DefaultRenderModes.Server
            @rendermode Microsoft.AspNetCore.Components.DefaultRenderModes.Server
            """);

        // Assert
        //Error RZ2001: The 'rendermode' directive may only occur once per document.
        var diagnostic = Assert.Single(compilationResult.Diagnostics);
        Assert.Equal("RZ2001", diagnostic.Id);
    }

    [Fact]
    public void RenderMode_With_InvalidIdentifier()
    {
        var compilationResult = CompileToCSharp("""
            @rendermode NoExist
            """);

        Assert.Empty(compilationResult.Diagnostics);

        var assemblyResult = CompileToAssembly(compilationResult, throwOnFailure: false);
        assemblyResult.Diagnostics.Verify(
            // x:\dir\subdir\Test\TestComponent.cshtml(1,13): error CS0103: The name 'NoExist' does not exist in the current context
            //             NoExist
            Diagnostic(ErrorCode.ERR_NameNotInContext, "NoExist").WithArguments("NoExist").WithLocation(1, 13)
            );
    }

    [Fact]
    public void LanguageVersion()
    {
        var compilationResult = CompileToCSharp("""
            @rendermode Microsoft.AspNetCore.Components.DefaultRenderModes.Server
            """, configuration: Configuration.WithVersion(RazorLanguageVersion.Version_7_0));

        Assert.Empty(compilationResult.Diagnostics);

        var assemblyResult = CompileToAssembly(compilationResult, throwOnFailure: false);
        assemblyResult.Diagnostics.Verify(
            // x:\dir\subdir\Test\TestComponent.cshtml(1,3): error CS0103: The name 'rendermode' does not exist in the current context
            // __builder.AddContent(0, rendermode);
            Diagnostic(ErrorCode.ERR_NameNotInContext, "rendermode").WithArguments("rendermode").WithLocation(1, 3)
            );
    }

    [Fact]
    public void LanguageVersion_BreakingChange_7_0()
    {
        var compilationResult = CompileToCSharp("""
            @rendermode Foo

            @code
            {
                string rendermode = "Something";
            }
            """, configuration: Configuration.WithVersion(RazorLanguageVersion.Version_7_0));

        Assert.Empty(compilationResult.Diagnostics);

        var assemblyResult = CompileToAssembly(compilationResult, throwOnFailure: true);
        assemblyResult.Diagnostics.Verify();
    }

    [Fact]
    public void LanguageVersion_BreakingChange_8_0()
    {
        var compilationResult = CompileToCSharp("""
            @rendermode Foo

            @code
            {
                string rendermode = "Something";
            }
            """, configuration: Configuration.WithVersion(RazorLanguageVersion.Version_8_0));

        Assert.Empty(compilationResult.Diagnostics);

        var assemblyResult = CompileToAssembly(compilationResult, throwOnFailure: false);
        assemblyResult.Diagnostics.Verify(
            // x:\dir\subdir\Test\TestComponent.cshtml(1,13): error CS0103: The name 'Foo' does not exist in the current context
            //             Foo
            Diagnostic(ErrorCode.ERR_NameNotInContext, "Foo").WithArguments("Foo").WithLocation(1, 13),
            // x:\dir\subdir\Test\TestComponent.cshtml(5,12): warning CS0414: The field 'TestComponent.rendermode' is assigned but its value is never used
            //     string rendermode = "Something";
            Diagnostic(ErrorCode.WRN_UnreferencedFieldAssg, "rendermode").WithArguments("Test.TestComponent.rendermode").WithLocation(5, 12)
            );
    }

    internal static Compilation AddRequiredAttributes(Compilation compilation) => compilation.AddSyntaxTrees(Parse(RenderModeAttribute, path: "RuntimeAttributes.cs"));

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
            public static IComponentRenderMode Server = new DefaultRenderModes();
         }
         """;
}

