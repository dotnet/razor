﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.CodeAnalysis;
using Xunit;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

public class ViewComponentTagHelperDescriptorFactoryTest
{
    private static readonly Assembly _assembly = typeof(ViewComponentTagHelperDescriptorFactoryTest).GetTypeInfo().Assembly;

    [Fact]
    public void CreateDescriptor_UnderstandsStringParameters()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(StringParameterViewComponent).FullName);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var expectedDescriptor = TagHelperDescriptorBuilder.Create(
            ViewComponentTagHelperConventions.Kind,
            "__Generated__StringParameterViewComponentTagHelper",
            typeof(StringParameterViewComponent).GetTypeInfo().Assembly.GetName().Name)
            .Metadata(
                TypeName("__Generated__StringParameterViewComponentTagHelper"),
                new(ViewComponentTagHelperMetadata.Name, "StringParameter"))
            .DisplayName("StringParameterViewComponentTagHelper")
            .TagMatchingRuleDescriptor(rule =>
                rule
                .RequireTagName("vc:string-parameter")
                .RequireAttributeDescriptor(attribute => attribute.Name("foo"))
                .RequireAttributeDescriptor(attribute => attribute.Name("bar")))
            .BoundAttributeDescriptor(attribute =>
                attribute
                .Name("foo")
                .Metadata(PropertyName("foo"))
                .TypeName(typeof(string).FullName)
                .DisplayName("string StringParameterViewComponentTagHelper.foo"))
            .BoundAttributeDescriptor(attribute =>
                attribute
                .Name("bar")
                .Metadata(PropertyName("bar"))
                .TypeName(typeof(string).FullName)
                .DisplayName("string StringParameterViewComponentTagHelper.bar"))
            .Build();

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        Assert.Equal(expectedDescriptor, descriptor, TagHelperDescriptorComparer.Default);
    }

    [Fact]
    public void CreateDescriptor_UnderstandsVariousParameterTypes()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(VariousParameterViewComponent).FullName);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var expectedDescriptor = TagHelperDescriptorBuilder.Create(
            ViewComponentTagHelperConventions.Kind,
            "__Generated__VariousParameterViewComponentTagHelper",
            typeof(VariousParameterViewComponent).GetTypeInfo().Assembly.GetName().Name)
            .Metadata(
                TypeName("__Generated__VariousParameterViewComponentTagHelper"),
                new(ViewComponentTagHelperMetadata.Name, "VariousParameter"))
            .DisplayName("VariousParameterViewComponentTagHelper")
            .TagMatchingRuleDescriptor(rule =>
                rule
                .RequireTagName("vc:various-parameter")
                .RequireAttributeDescriptor(attribute => attribute.Name("test-enum"))
                .RequireAttributeDescriptor(attribute => attribute.Name("test-string")))
            .BoundAttributeDescriptor(attribute =>
                attribute
                .Name("test-enum")
                .Metadata(PropertyName("testEnum"))
                .TypeName(typeof(VariousParameterViewComponent).FullName + "." + nameof(VariousParameterViewComponent.TestEnum))
                .AsEnum()
                .DisplayName(typeof(VariousParameterViewComponent).FullName + "." + nameof(VariousParameterViewComponent.TestEnum) + " VariousParameterViewComponentTagHelper.testEnum"))
            .BoundAttributeDescriptor(attribute =>
                attribute
                .Name("test-string")
                .Metadata(PropertyName("testString"))
                .TypeName(typeof(string).FullName)
                .DisplayName("string VariousParameterViewComponentTagHelper.testString"))
            .BoundAttributeDescriptor(attribute =>
                attribute
                .Name("baz")
                .Metadata(PropertyName("baz"))
                .TypeName(typeof(int).FullName)
                .DisplayName("int VariousParameterViewComponentTagHelper.baz"))
            .Build();

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        Assert.Equal(expectedDescriptor, descriptor, TagHelperDescriptorComparer.Default);
    }

    [Fact]
    public void CreateDescriptor_UnderstandsGenericParameters()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(GenericParameterViewComponent).FullName);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var expectedDescriptor = TagHelperDescriptorBuilder.Create(
            ViewComponentTagHelperConventions.Kind,
            "__Generated__GenericParameterViewComponentTagHelper",
            typeof(GenericParameterViewComponent).GetTypeInfo().Assembly.GetName().Name)
            .Metadata(
                TypeName("__Generated__GenericParameterViewComponentTagHelper"),
                new(ViewComponentTagHelperMetadata.Name, "GenericParameter"))
            .DisplayName("GenericParameterViewComponentTagHelper")
            .TagMatchingRuleDescriptor(rule =>
                rule
                .RequireTagName("vc:generic-parameter")
                .RequireAttributeDescriptor(attribute => attribute.Name("foo")))
            .BoundAttributeDescriptor(attribute =>
                attribute
                .Name("foo")
                .Metadata(PropertyName("Foo"))
                .TypeName("System.Collections.Generic.List<System.String>")
                .DisplayName("System.Collections.Generic.List<System.String> GenericParameterViewComponentTagHelper.Foo"))
            .BoundAttributeDescriptor(attribute =>
                attribute
                .Name("bar")
                .Metadata(PropertyName("Bar"))
                .TypeName("System.Collections.Generic.Dictionary<System.String, System.Int32>")
                .AsDictionaryAttribute("bar-", typeof(int).FullName)
                .DisplayName("System.Collections.Generic.Dictionary<System.String, System.Int32> GenericParameterViewComponentTagHelper.Bar"))
            .Build();

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        Assert.Equal(expectedDescriptor, descriptor, TagHelperDescriptorComparer.Default);
    }

    [Fact]
    public void CreateDescriptor_ForSyncViewComponentWithInvokeInBaseType_Works()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var expectedDescriptor = TagHelperDescriptorBuilder.Create(
            ViewComponentTagHelperConventions.Kind,
            "__Generated__SyncDerivedViewComponentTagHelper",
            typeof(SyncDerivedViewComponent).GetTypeInfo().Assembly.GetName().Name)
            .Metadata(
                TypeName("__Generated__SyncDerivedViewComponentTagHelper"),
                new(ViewComponentTagHelperMetadata.Name, "SyncDerived"))
            .DisplayName("SyncDerivedViewComponentTagHelper")
            .TagMatchingRuleDescriptor(rule =>
                rule
                .RequireTagName("vc:sync-derived")
                .RequireAttributeDescriptor(attribute => attribute.Name("foo"))
                .RequireAttributeDescriptor(attribute => attribute.Name("bar")))
            .BoundAttributeDescriptor(attribute =>
                attribute
                .Name("foo")
                .Metadata(PropertyName("foo"))
                .TypeName(typeof(string).FullName)
                .DisplayName("string SyncDerivedViewComponentTagHelper.foo"))
            .BoundAttributeDescriptor(attribute =>
                attribute
                .Name("bar")
                .Metadata(PropertyName("bar"))
                .TypeName(typeof(string).FullName)
                .DisplayName("string SyncDerivedViewComponentTagHelper.bar"))
            .Build();

        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(SyncDerivedViewComponent).FullName);

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        Assert.Equal(expectedDescriptor, descriptor, TagHelperDescriptorComparer.Default);
    }

    [Fact]
    public void CreateDescriptor_ForAsyncViewComponentWithInvokeInBaseType_Works()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var expectedDescriptor = TagHelperDescriptorBuilder.Create(
            ViewComponentTagHelperConventions.Kind,
            "__Generated__AsyncDerivedViewComponentTagHelper",
            typeof(AsyncDerivedViewComponent).Assembly.GetName().Name)
            .Metadata(
                TypeName("__Generated__AsyncDerivedViewComponentTagHelper"),
                new(ViewComponentTagHelperMetadata.Name, "AsyncDerived"))
            .DisplayName("AsyncDerivedViewComponentTagHelper")
            .TagMatchingRuleDescriptor(rule => rule.RequireTagName("vc:async-derived"))
            .Build();

        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(AsyncDerivedViewComponent).FullName);

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        Assert.Equal(expectedDescriptor, descriptor, TagHelperDescriptorComparer.Default);
    }

    [Fact]
    public void CreateDescriptor_AddsDiagnostic_ForViewComponentWithNoInvokeMethod()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(ViewComponentWithoutInvokeMethod).FullName);

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        var diagnostic = Assert.Single(descriptor.GetAllDiagnostics());
        Assert.Equal(RazorExtensionsDiagnosticFactory.ViewComponent_CannotFindMethod.Id, diagnostic.Id);
    }

    [Fact]
    public void CreateDescriptor_AddsDiagnostic_ForViewComponentWithNoInstanceInvokeMethod()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(StaticInvokeAsyncViewComponent).FullName);

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        var diagnostic = Assert.Single(descriptor.GetAllDiagnostics());
        Assert.Equal(RazorExtensionsDiagnosticFactory.ViewComponent_CannotFindMethod.Id, diagnostic.Id);
    }

    [Fact]
    public void CreateDescriptor_AddsDiagnostic_ForViewComponentWithNoPublicInvokeMethod()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(NonPublicInvokeAsyncViewComponent).FullName);

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        var diagnostic = Assert.Single(descriptor.GetAllDiagnostics());
        Assert.Equal(RazorExtensionsDiagnosticFactory.ViewComponent_CannotFindMethod.Id, diagnostic.Id);
    }

    [Fact]
    public void CreateDescriptor_ForViewComponentWithInvokeAsync_UnderstandsGenericTask()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(AsyncViewComponentWithGenericTask).FullName);

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        Assert.Empty(descriptor.GetAllDiagnostics());
    }

    [Fact]
    public void CreateDescriptor_ForViewComponentWithInvokeAsync_UnderstandsNonGenericTask()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(AsyncViewComponentWithNonGenericTask).FullName);

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        Assert.Empty(descriptor.GetAllDiagnostics());
    }

    [Fact]
    public void CreateDescriptor_ForViewComponentWithInvokeAsync_DoesNotUnderstandVoid()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(AsyncViewComponentWithString).FullName);

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        var diagnostic = Assert.Single(descriptor.GetAllDiagnostics());
        Assert.Equal(RazorExtensionsDiagnosticFactory.ViewComponent_AsyncMethod_ShouldReturnTask.Id, diagnostic.Id);
    }

    [Fact]
    public void CreateDescriptor_ForViewComponentWithInvokeAsync_DoesNotUnderstandString()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(AsyncViewComponentWithString).FullName);

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        var diagnostic = Assert.Single(descriptor.GetAllDiagnostics());
        Assert.Equal(RazorExtensionsDiagnosticFactory.ViewComponent_AsyncMethod_ShouldReturnTask.Id, diagnostic.Id);
    }

    [Fact]
    public void CreateDescriptor_ForViewComponentWithInvoke_DoesNotUnderstandVoid()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(SyncViewComponentWithVoid).FullName);

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        var diagnostic = Assert.Single(descriptor.GetAllDiagnostics());
        Assert.Equal(RazorExtensionsDiagnosticFactory.ViewComponent_SyncMethod_ShouldReturnValue.Id, diagnostic.Id);
    }

    [Fact]
    public void CreateDescriptor_ForViewComponentWithInvoke_DoesNotUnderstandNonGenericTask()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(SyncViewComponentWithNonGenericTask).FullName);

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        var diagnostic = Assert.Single(descriptor.GetAllDiagnostics());
        Assert.Equal(RazorExtensionsDiagnosticFactory.ViewComponent_SyncMethod_CannotReturnTask.Id, diagnostic.Id);
    }

    [Fact]
    public void CreateDescriptor_ForViewComponentWithInvoke_DoesNotUnderstandGenericTask()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(SyncViewComponentWithGenericTask).FullName);

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        var diagnostic = Assert.Single(descriptor.GetAllDiagnostics());
        Assert.Equal(RazorExtensionsDiagnosticFactory.ViewComponent_SyncMethod_CannotReturnTask.Id, diagnostic.Id);
    }

    [Fact]
    public void CreateDescriptor_ForViewComponent_WithAmbiguousMethods()
    {
        // Arrange
        var testCompilation = TestCompilation.Create(_assembly);
        var factory = new ViewComponentTagHelperDescriptorFactory(testCompilation);

        var viewComponent = testCompilation.GetTypeByMetadataName(typeof(DerivedViewComponentWithAmbiguity).FullName);

        // Act
        var descriptor = factory.CreateDescriptor(viewComponent);

        // Assert
        var diagnostic = Assert.Single(descriptor.GetAllDiagnostics());
        Assert.Equal(RazorExtensionsDiagnosticFactory.ViewComponent_AmbiguousMethods.Id, diagnostic.Id);
    }
}

public class StringParameterViewComponent
{
    public string Invoke(string foo, string bar) => null;
}

public class VariousParameterViewComponent
{
    public string Invoke(TestEnum testEnum, string testString, int baz = 5) => null;

    public enum TestEnum
    {
        A = 1,
        B = 2,
        C = 3
    }
}

public class GenericParameterViewComponent
{
    public string Invoke(List<string> Foo, Dictionary<string, int> Bar) => null;
}

public class ViewComponentWithoutInvokeMethod
{
}

public class AsyncViewComponentWithGenericTask
{
    public Task<string> InvokeAsync() => null;
}

public class AsyncViewComponentWithNonGenericTask
{
    public Task InvokeAsync() => null;
}

public class AsyncViewComponentWithVoid
{
    public void InvokeAsync() { }
}

public class AsyncViewComponentWithString
{
    public string InvokeAsync() => null;
}

public class SyncViewComponentWithVoid
{
    public void Invoke() { }
}

public class SyncViewComponentWithNonGenericTask
{
    public Task Invoke() => null;
}

public class SyncViewComponentWithGenericTask
{
    public Task<string> Invoke() => null;
}

public class SyncDerivedViewComponent : StringParameterViewComponent
{
}

public class AsyncDerivedViewComponent : AsyncViewComponentWithNonGenericTask
{
}

public class DerivedViewComponentWithAmbiguity : AsyncViewComponentWithNonGenericTask
{
    public string Invoke() => null;
}

public class StaticInvokeAsyncViewComponent
{
    public static Task<string> InvokeAsync() => null;
}

public class NonPublicInvokeAsyncViewComponent
{
    protected Task<string> InvokeAsync() => null;
}
