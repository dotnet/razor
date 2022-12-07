// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.Generator;

public class RazorTests
{
    [Fact]
    public void Test_Project_Load_Cold()
    {
        // arrange
        var razorBenchmarks = new RazorBenchmarks();

        // act
        razorBenchmarks.Startup = AbstractBenchmark.StartupKind.Cold;
        razorBenchmarks.Setup();

        // assert
        var project = razorBenchmarks.Project;
        Assert.NotNull(project);
        Assert.NotNull(project.GeneratorDriver);
        Assert.NotNull(project.OptionsProvider);
        Assert.NotNull(project.Compilation);
        Assert.NotNull(project.ParseOptions);

        Assert.Equal(110, project.AdditionalTexts.Length);
        Assert.Equal(8, project.Compilation.SyntaxTrees.Count());

        // Generator driver will throw if it's not been run yet. This checks we're in a cold state.
        Assert.Throws<NullReferenceException>(() => project.GeneratorDriver.GetRunResult());
    }

    [Fact]
    public void Test_Project_Load_Warm()
    {
        // arrange
        var razorBenchmarks = new RazorBenchmarks();

        // act
        razorBenchmarks.Startup = AbstractBenchmark.StartupKind.Warm;
        razorBenchmarks.Setup();

        // assert
        var project = razorBenchmarks.Project;
        Assert.NotNull(project);

        var results = project.GeneratorDriver.GetRunResult();
        Assert.Empty(results.Diagnostics);
        Assert.Equal(110, results.GeneratedTrees.Length);
    }

}
