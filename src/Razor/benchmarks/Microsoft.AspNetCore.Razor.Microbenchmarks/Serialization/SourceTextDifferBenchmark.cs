// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT license. See License.txt in the project root for license information.

using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Razor.LanguageServer;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.AspNetCore.Razor.Microbenchmarks.Serialization;

public class SourceTextDifferBenchmark
{
    private SourceText? _largeFileOriginal;
    private SourceText? _largeFileMinimalChanges;
    private SourceText? _largeFileSignificantChanges;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var largeFileText = Resources.GetResourceText("MSN.cshtml");

        _largeFileOriginal = SourceText.From(largeFileText);

        var changedText = largeFileText.Insert(100, "<");
        _largeFileMinimalChanges = SourceText.From(changedText);

        changedText = largeFileText.Substring(largeFileText.Length / 2).Reverse().ToString();
        _largeFileSignificantChanges = SourceText.From(changedText);
    }

    [Benchmark(Description = "Line Diff - One line change (Typing)")]
    public void LineDiff_LargeFile_OneLineChanged()
    {
        SourceTextDiffer.GetMinimalTextChanges(_largeFileOriginal!, _largeFileMinimalChanges!, lineDiffOnly: true);
    }

    [Benchmark(Description = "Line Diff - Significant Changes (Copy-paste)")]
    public void LineDiff_LargeFile_SignificantlyDifferent()
    {
        SourceTextDiffer.GetMinimalTextChanges(_largeFileOriginal!, _largeFileSignificantChanges!, lineDiffOnly: true);
    }

    [Benchmark(Description = "Character Diff - One character change (Typing)")]
    public void CharDiff_LargeFile_OneCharChanged()
    {
        SourceTextDiffer.GetMinimalTextChanges(_largeFileOriginal!, _largeFileMinimalChanges!, lineDiffOnly: false);
    }
}
