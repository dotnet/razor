// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions;

public class MvcImportProjectFeatureTest
{
    [Fact]
    public void AddDefaultDirectivesImport_AddsSingleDynamicImport()
    {
        // Arrange
        using var imports = new PooledArrayBuilder<RazorProjectItem>();

        // Act
        MvcImportProjectFeature.AddDefaultDirectivesImport(suppressMvcRazorImports: false, ref imports.AsRef());

        // Assert
        var import = Assert.Single(imports.ToImmutable());
        Assert.Null(import.FilePath);
    }

    [Fact]
    public void AddDefaultDirectivesImport_DefaultImportContainsMvcDirectives()
    {
        // Arrange
        using var imports = new PooledArrayBuilder<RazorProjectItem>();

        // Act
        MvcImportProjectFeature.AddDefaultDirectivesImport(suppressMvcRazorImports: false, ref imports.AsRef());

        // Assert
        var import = Assert.Single(imports.ToImmutable());
        var content = ReadContent(import);
        Assert.Contains("@inject", content);
        Assert.Contains("@addTagHelper", content);
        Assert.Contains("@using global::Microsoft.AspNetCore.Mvc", content);
    }

    [Fact]
    public void AddDefaultDirectivesImport_SuppressMvcRazorImports_OmitsMvcDirectives()
    {
        // Arrange
        using var imports = new PooledArrayBuilder<RazorProjectItem>();

        // Act
        MvcImportProjectFeature.AddDefaultDirectivesImport(suppressMvcRazorImports: true, ref imports.AsRef());

        // Assert
        var import = Assert.Single(imports.ToImmutable());
        var content = ReadContent(import);
        Assert.DoesNotContain("@inject", content);
        Assert.DoesNotContain("@addTagHelper", content);
        Assert.DoesNotContain("Microsoft.AspNetCore.Mvc", content);
        Assert.Contains("@using global::System", content);
        Assert.Contains("@using global::System.Collections.Generic", content);
        Assert.Contains("@using global::System.Linq", content);
        Assert.Contains("@using global::System.Threading.Tasks", content);
    }

    [Fact]
    public void AddHierarchicalImports_AddsViewImportSourceDocumentsOnDisk()
    {
        // Arrange
        using var imports = new PooledArrayBuilder<RazorProjectItem>();
        var projectItem = new TestRazorProjectItem("/Contact/Index.cshtml");
        var testFileSystem = new TestRazorProjectFileSystem(new[]
        {
            new TestRazorProjectItem("/Index.cshtml"),
            new TestRazorProjectItem("/_ViewImports.cshtml"),
            new TestRazorProjectItem("/Contact/_ViewImports.cshtml"),
            projectItem,
        });
        var mvcImportFeature = new MvcImportProjectFeature()
        {
            ProjectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, testFileSystem)
        };

        // Act
        mvcImportFeature.AddHierarchicalImports(projectItem, ref imports.AsRef());

        // Assert
        Assert.Collection(imports.ToImmutable(),
            import => Assert.Equal("/_ViewImports.cshtml", import.FilePath),
            import => Assert.Equal("/Contact/_ViewImports.cshtml", import.FilePath));
    }

    [Fact]
    public void AddHierarchicalImports_AddsViewImportSourceDocumentsNotOnDisk()
    {
        // Arrange
        using var imports = new PooledArrayBuilder<RazorProjectItem>();
        var projectItem = new TestRazorProjectItem("/Pages/Contact/Index.cshtml");
        var testFileSystem = new TestRazorProjectFileSystem(new[] { projectItem });
        var mvcImportFeature = new MvcImportProjectFeature()
        {
            ProjectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, testFileSystem)
        };

        // Act
        mvcImportFeature.AddHierarchicalImports(projectItem, ref imports.AsRef());

        // Assert
        Assert.Collection(imports.ToImmutable(),
            import => Assert.Equal("/_ViewImports.cshtml", import.FilePath),
            import => Assert.Equal("/Pages/_ViewImports.cshtml", import.FilePath),
            import => Assert.Equal("/Pages/Contact/_ViewImports.cshtml", import.FilePath));
    }

    private static string ReadContent(RazorProjectItem item)
    {
        using var stream = item.Read();
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }
}
