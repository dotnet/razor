// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using static Microsoft.AspNetCore.Razor.Microbenchmarks.Generator.ProjectSetup;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.Generator;

public class Benchmarks
{
    ProjectSetup.RazorProject? _project;

    public enum ChangeKind { Independent, DependentIgnorable, Dependent };
    [ParamsAllExceptDebug(ChangeKind.Independent)]
    public ChangeKind Change { get; set; }

    public enum StartupKind { Warm, Cold };
    [ParamsAllExceptDebug(StartupKind.Warm)]
    public StartupKind Startup { get; set; }

    public enum FileTypeKind { CSharp, Razor };
    [ParamsAllExceptDebug(FileTypeKind.Razor)]
    public FileTypeKind FileType { get; set; }

    public enum OperationKind { AddFile, EditFile, RemoveFile };
    [ParamsAllExceptDebug(OperationKind.EditFile)]
    public OperationKind Operation { get; set; }

    [ModuleInitializer]
    public static void LoadMSBuild() => MSBuildLocator.RegisterDefaults();

    [GlobalSetup]
    public void Setup()
    {
        _project = ProjectSetup.GetRazorProject(cold: Startup == StartupKind.Cold);
    }

    [Benchmark]
    public void RunBenchmark()
    {
        var compilation = _project!.Compilation;
        var driver = _project!.GeneratorDriver;
        if (FileType == FileTypeKind.CSharp)
        {
            compilation = CSharpChange();
        }
        else
        {
            driver = RazorChange();
        }

        driver = driver.RunGenerators(compilation);
        var result = driver.GetRunResult();

        Debug.Assert(result.Diagnostics.IsDefaultOrEmpty);
    }

    private GeneratorDriver RazorChange()
    {
        var driver = _project!.GeneratorDriver;
        var newRazorFile = GetNewRazorFile();
        var existingRazorFile = GetExistingRazorFile();

        if (newRazorFile is not null && existingRazorFile is not null)
        {
            driver = driver.ReplaceAdditionalText(existingRazorFile, newRazorFile);
        }
        else if (newRazorFile is not null)
        {
            driver = driver.AddAdditionalTexts(ImmutableArray.Create(newRazorFile));
        }
        else if (existingRazorFile is not null)
        {
            driver = driver.RemoveAdditionalTexts(ImmutableArray.Create(existingRazorFile));
        }

        return driver;
    }

    private AdditionalText? GetNewRazorFile()
    {
        if (Change == ChangeKind.Independent)
        {
            return new InMemoryAdditionalText(IndependentRazorFile, "Pages/Generated/0.razor");
        }
        else if (Change == ChangeKind.DependentIgnorable)
        {
            return new InMemoryAdditionalText(DependentIgnorableRazorFile, "Pages/Counter.razor");
        }
        else
        {
            return new InMemoryAdditionalText(DependentRazorFile, "Pages/Counter.razor");
        }
    }

    private AdditionalText? GetExistingRazorFile()
    {
        if (Change != ChangeKind.Independent)
        {
            return _project!.AdditionalTexts.Single(a => a.Path.EndsWith("Counter.razor", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            return _project!.AdditionalTexts.Single(a => a.Path.EndsWith("0.razor", StringComparison.OrdinalIgnoreCase));
        }
    }

    private Compilation CSharpChange()
    {
        var compilation = _project!.Compilation;
        var newSyntaxTree = GetNewTree();
        var existingSyntaxTree = GetExistingTree();

        if (newSyntaxTree is not null)
        {
            compilation = compilation.AddSyntaxTrees(newSyntaxTree);
        }
        if (existingSyntaxTree is not null)
        {
            compilation = compilation.RemoveSyntaxTrees(existingSyntaxTree);
        }

        return compilation;
    }

    private SyntaxTree? GetNewTree()
    {
        if (Change == ChangeKind.Independent)
        {
            return CSharpSyntaxTree.ParseText("public class D{}", _project!.ParseOptions);
        }
        return null;
    }

    private SyntaxTree? GetExistingTree()
    {
        if (Operation == OperationKind.AddFile)
        {
            return null;
        }
        return null;
    }

    private const string IndependentRazorFile = "<h1>Independent File</h1>";

    private const string DependentIgnorableRazorFile = """
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

    private const string DependentRazorFile = """
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
