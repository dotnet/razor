// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.Generator;

public class RazorBenchmarks : AbstractBenchmark
{
    [Benchmark]
    public void Razor_Add_Independent() => RunRazorBenchmark(new(Independent, "Independent.razor"), null);

    [Benchmark]
    public void Razor_Edit_Independent() => RunRazorBenchmark(new(Independent, "\\Pages\\Generated\\0.razor"), "\\0.razor");

    [Benchmark]
    public void Razor_Remove_Independent() => RunRazorBenchmark(null, "\\0.razor");

    [Benchmark]
    public void Razor_Edit_DependentIgnorable() => RunRazorBenchmark(new(DependentIgnorable, "\\Pages\\Counter.razor"), "Counter.razor");

    [Benchmark]
    public void Razor_Edit_Dependent() => RunRazorBenchmark(new(Dependent, "\\Pages\\Counter.razor"), "Counter.razor");

    [Benchmark]
    public void Razor_Remove_Dependent() => RunRazorBenchmark(null, "\\Counter.razor");


    private void RunRazorBenchmark(ProjectSetup.InMemoryAdditionalText? AddedFile, string? RemovedFileSuffix) => RunBenchmark((ProjectSetup.RazorProject project) =>
    {
        var removedFile = RemovedFileSuffix is not null
                            ? project.AdditionalTexts.Single(a => a.Path.EndsWith(RemovedFileSuffix, StringComparison.OrdinalIgnoreCase))
                            : null;

        if (AddedFile is not null && removedFile is not null)
        {
            return project.GeneratorDriver.ReplaceAdditionalText(removedFile, AddedFile);
        }
        else if (AddedFile is not null)
        {
            return project.GeneratorDriver.AddAdditionalTexts(ImmutableArray.Create((AdditionalText)AddedFile));
        }
        else if (removedFile is not null)
        {
            return project.GeneratorDriver.RemoveAdditionalTexts(ImmutableArray.Create(removedFile));
        }

        return project.GeneratorDriver;
    });


    private const string Independent = "<h1>Independent file</h1>";

    private const string DependentIgnorable = """
        @page "/counter"

        <PageTitle>Counter edited</PageTitle>

        <h1>Counter</h1>

        <p role="status">Current count: @currentCount</p>

        <button class="btn btn-primary" @onclick="IncrementCount">Click me</button>

        @code {
            [Parameter]
            public int IncrementAmount { get; set; } = 1; 

            private int currentCount = 0;

            private void IncrementCount()
            {
                currentCount += IncrementAmount;
            }
        }

        """;

    private const string Dependent = """
    @page "/counter"

    <PageTitle>Counter edited</PageTitle>

    <h1>Counter</h1>

    <p role="status">Current count: @currentCount</p>

    <button class="btn btn-primary" @onclick="IncrementCount">Click me</button>

    @code {

        private int currentCount = 0;

        private void IncrementCount()
        {
            currentCount++;
        }
    }

    """;
}
