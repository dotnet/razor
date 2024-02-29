﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Collections.Generic;
using Microsoft.AspNetCore.Razor.Language;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Razor.Extensions.Version1_X;

public class MvcImportProjectFeatureTest
{
    [Fact]
    public void AddDefaultDirectivesImport_AddsSingleDynamicImport()
    {
        // Arrange
        var imports = new List<RazorProjectItem>();

        // Act
        MvcImportProjectFeature.AddDefaultDirectivesImport(imports);

        // Assert
        var import = Assert.Single(imports);
        Assert.Null(import.FilePath);
    }

    [Fact]
    public void AddHierarchicalImports_AddsViewImportSourceDocumentsOnDisk()
    {
        // Arrange
        var imports = new List<RazorProjectItem>();
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
        mvcImportFeature.AddHierarchicalImports(projectItem, imports);

        // Assert
        Assert.Collection(imports,
            import => Assert.Equal("/_ViewImports.cshtml", import.FilePath),
            import => Assert.Equal("/Contact/_ViewImports.cshtml", import.FilePath));
    }

    [Fact]
    public void AddHierarchicalImports_AddsViewImportSourceDocumentsNotOnDisk()
    {
        // Arrange
        var imports = new List<RazorProjectItem>();
        var projectItem = new TestRazorProjectItem("/Pages/Contact/Index.cshtml");
        var testFileSystem = new TestRazorProjectFileSystem(new[] { projectItem });
        var mvcImportFeature = new MvcImportProjectFeature()
        {
            ProjectEngine = RazorProjectEngine.Create(RazorConfiguration.Default, testFileSystem)
        };

        // Act
        mvcImportFeature.AddHierarchicalImports(projectItem, imports);

        // Assert
        Assert.Collection(imports,
            import => Assert.Equal("/_ViewImports.cshtml", import.FilePath),
            import => Assert.Equal("/Pages/_ViewImports.cshtml", import.FilePath),
            import => Assert.Equal("/Pages/Contact/_ViewImports.cshtml", import.FilePath));
    }
}
