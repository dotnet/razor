// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Running;

Job baseJob = Job.Default;
#if DEBUG
baseJob = baseJob
        .WithIterationCount(1)
        .RunOncePerIteration()
        .WithToolchain(new BenchmarkDotNet.Toolchains.InProcess.Emit.InProcessEmitToolchain(TimeSpan.FromHours(1.0), logOutput: true));
#endif

var config = ManualConfig.CreateMinimumViable()
            .AddJob(baseJob.WithCustomBuildConfiguration("Release").WithId("Current"))
            .AddJob(baseJob.WithCustomBuildConfiguration("Release_Nuget").WithId("Baseline").WithBaseline(true))
            .StopOnFirstError(true)
            .AddExporter(CsvExporter.Default);

var results = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);

var reports =
    from summary in results
    from report in summary.Reports
    where !summary.IsBaseline(report.BenchmarkCase)
    let baselineCase = summary.GetBaseline(summary.GetLogicalGroupKey(report.BenchmarkCase))
    let baseline = summary.Reports.Single(r => r.BenchmarkCase == baselineCase)
    select (report, baseline);
                                  

foreach ((var benchmark, var baseline) in reports)
{

    //benchmark.ResultStatistics.Mean

    // TODO: compare and decide on result
}
