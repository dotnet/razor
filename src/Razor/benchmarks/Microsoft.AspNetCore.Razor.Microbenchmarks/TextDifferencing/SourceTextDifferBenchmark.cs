// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Razor.Microbenchmarks.LanguageServer;
using Microsoft.AspNetCore.Razor.PooledObjects;
using Microsoft.AspNetCore.Razor.TextDifferencing;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.TextDifferencing;

public class SourceTextDifferBenchmark : RazorLanguageServerBenchmarkBase
{
    private SourceText? _largeFileOriginal;
    private SourceText? _largeFileMinimalChanges;
    private SourceText? _largeFileSignificantChanges;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var msnCshtmlDiskPath = Path.Combine(RepoRoot, "src", "Razor", "test", "testapps", "ComponentApp", "Components", "Pages", "MSN.cshtml");
        using var fileStream = new FileStream(msnCshtmlDiskPath, FileMode.Open);
        var reader = new StreamReader(fileStream);
        var largeFileText = reader.ReadToEnd();

        _largeFileOriginal = SourceText.From(largeFileText);

        var changedText = largeFileText.Insert(100, "<");
        _largeFileMinimalChanges = SourceText.From(changedText);

        // Reverse the last half of the file
        using var _ = StringBuilderPool.GetPooledObject(out var builder);

        foreach (var ch in largeFileText[(largeFileText.Length / 2)..].Reverse())
        {
            builder.Append(ch);
        }

        changedText = builder.ToString();
        _largeFileSignificantChanges = SourceText.From(changedText);
    }

    [Benchmark(Description = "Line Diff - One line change (Typing)")]
    public void LineDiff_LargeFile_OneLineChanged()
    {
        SourceTextDiffer.GetMinimalTextChanges(_largeFileOriginal!, _largeFileMinimalChanges!, DiffKind.Line);
    }

    [Benchmark(Description = "Line Diff - Significant Changes (Copy-paste)")]
    public void LineDiff_LargeFile_SignificantlyDifferent()
    {
        SourceTextDiffer.GetMinimalTextChanges(_largeFileOriginal!, _largeFileSignificantChanges!, DiffKind.Line);
    }

    [Benchmark(Description = "Character Diff - One character change (Typing)")]
    public void CharDiff_LargeFile_OneCharChanged()
    {
        SourceTextDiffer.GetMinimalTextChanges(_largeFileOriginal!, _largeFileMinimalChanges!, DiffKind.Char);
    }
}
