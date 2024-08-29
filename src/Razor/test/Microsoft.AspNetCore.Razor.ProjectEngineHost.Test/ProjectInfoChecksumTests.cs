// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.ProjectSystem;
using Microsoft.AspNetCore.Razor.Serialization;
using Microsoft.AspNetCore.Razor.Test.Common;
using Xunit;

namespace Microsoft.AspNetCore.Razor.ProjectEngineHost.Test;

public class ProjectInfoChecksumTests
{
    [Fact]
    public void CheckSame()
    {
        var info1 = CreateInfo();
        var info2 = CreateInfo();

        Assert.Equal(info1.Checksum, info2.Checksum);
    }

    [Fact]
    public void Change_ProjectKey()
    {
        var info1 = CreateInfo();
        var info2 = info1 with { ProjectKey = new ProjectKey("Test2") };

        Assert.NotEqual(info1.Checksum, info2.Checksum);
    }

    [Fact]
    public void Change_FilePath()
    {
        var info1 = CreateInfo();
        var info2 = info1 with { FilePath = @"C:\test\test2.csproj" };

        Assert.NotEqual(info1.Checksum, info2.Checksum);
    }

    [Fact]
    public void Change_RootNamespace()
    {
        var info1 = CreateInfo();
        var info2 = info1 with { RootNamespace = "TestNamespace2" };

        Assert.NotEqual(info1.Checksum, info2.Checksum);
    }

    [Fact]
    public void Change_DisplayName()
    {
        var info1 = CreateInfo();
        var info2 = info1 with { DisplayName = "Test2 (tfm)" };

        Assert.NotEqual(info1.Checksum, info2.Checksum);
    }

    [Fact]
    public void Change_Configuration()
    {
        var info1 = CreateInfo();
        var info2 = info1 with { Configuration = new RazorConfiguration(RazorLanguageVersion.Latest, "TestConfiguration2", []) };

        Assert.NotEqual(info1.Checksum, info2.Checksum);
    }

    [Fact]
    public void Change_ProjectWorkspaceState()
    {
        var info1 = CreateInfo();
        var info2 = info1 with { ProjectWorkspaceState = ProjectWorkspaceState.Create(RazorTestResources.BlazorServerAppTagHelpers, CodeAnalysis.CSharp.LanguageVersion.CSharp10) };

        Assert.NotEqual(info1.Checksum, info2.Checksum);
    }

    [Fact]
    public void Change_Documents()
    {
        var info1 = CreateInfo();
        var info2 = info1 with { Documents = info1.Documents.Add(new DocumentSnapshotHandle(@"C:\test\home.razor", @"C:\test\lib\net8.0", FileKinds.Component)) };

        Assert.NotEqual(info1.Checksum, info2.Checksum);
    }

    RazorProjectInfo CreateInfo()
    {
        return new RazorProjectInfo(
            new ProjectKey("Test"),
            @"C:\test\test.csproj",
            new RazorConfiguration(RazorLanguageVersion.Latest, "TestConfiguration", []),
            "TestNamespace",
            "Test (tfm)",
            ProjectWorkspaceState.Create(RazorTestResources.BlazorServerAppTagHelpers, CodeAnalysis.CSharp.LanguageVersion.Latest),
            []);
    }
}
